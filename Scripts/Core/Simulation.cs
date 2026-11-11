namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// One island's battle: orders, movement, capture and resupply. Combat lives in
/// <c>Simulation.Combat.cs</c>, defender AI in <c>Simulation.Ai.cs</c> and the enemy command's
/// counter-attacks and reinforcements in <c>Simulation.Command.cs</c>.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Fixed substep rate, so motion, collision and reloads don't depend on frame rate.</summary>
    private const float SubstepSeconds = 1f / 60;

    /// <summary>A longer frame (a hitch, a breakpoint) is truncated to this rather than simulated.</summary>
    private const float MaxFrameSeconds = .25f;

    /// <summary>Spacing of the 2 × 2 formation a group forms around an ordered point, in metres.</summary>
    private const float FormationSpacing = 7;

    // Regroup places followers in rows behind the leader.
    private const float RegroupFirstRowDistance = 8;
    private const float RegroupRowSpacing = 7;
    private const float RegroupColumnSpacing = 8;

    // Speed changes per second while driven manually and while following a route.
    private const float ManualAcceleration = 15;
    private const float RouteAcceleration = 14;

    /// <summary>A waypoint counts as reached within this many metres.</summary>
    private const float WaypointReached = .7f;

    /// <summary>Radians off-heading beyond which a hull turns in place rather than driving.</summary>
    private const float MaxDriveAngle = .55f;

    // A vehicle making no progress replans after this long, staggered by id so a jam unpicks in turn.
    private const float StuckReplanSeconds = .8f;
    private const float StuckReplanStagger = .2f;

    // Below these, a vehicle counts as stationary / as having moved nowhere.
    private const float StationarySpeed = .1f;
    private const float MinimumProgress = .1f;
    private const float MovingThreshold = .001f;

    // Beach resupply rates for a stationary friendly vehicle out of contact.
    private const float RepairPerSecond = 1.8f;
    private const float RearmPerSecond = 2;

    /// <summary>Seeds the dice for Classic hits and defender timing, so a battle replays the same way given the same orders.</summary>
    public const int DefaultSeed = 1992;

    private readonly Random random;
    private int nextShell;

    public Simulation(Campaign? campaign = null, int seed = DefaultSeed)
    {
        Campaign = campaign?.Clone() ?? new Campaign();
        if (!Campaign.Valid())
        {
            throw new ArgumentException("Invalid campaign state.");
        }

        random = new(seed);
        Obstacles = Mission.Obstacles(Campaign.Island);
        SetUpBuildings();
        Terrain = Classic ? TerrainMap.For(Campaign.Island) : null;
        Navigation = new(Obstacles, Terrain);
        Recon = new(this);
        ReinforcementWaves = Classic ? Mission.ZoneBattle(Campaign)?.Columns?.Count ?? 0 : Mission.ReinforcementWaves[Campaign.Island];
        Sites = Mission.Sites(Campaign);
        ZoneBattle = Mission.ZoneBattle(Campaign);
        Vehicles = Mission.Vehicles(Campaign);
        SetUpSupport();
    }

    public Campaign Campaign { get; }

    public bool CanOrder => Phase == MissionPhase.Active;
    public bool Classic => Campaign.Mode == GameMode.Classic;

    /// <summary>Extra terms this battle must be won on (Classic mode).</summary>
    public BattleConditions Conditions => Mission.Conditions(Campaign);

    /// <summary>Whether the battle's objective is achieved: every site held, the target down, or every enemy destroyed.</summary>
    public bool ObjectiveMet() => ZoneBattle?.Objective switch
    {
        ObjectiveKind.Destroy => TargetHealth <= 0,
        ObjectiveKind.Clear => !Vehicles.Any(v => v.Enemy && v.Alive),
        ObjectiveKind.Hold => Elapsed >= ZoneBattle.HoldSeconds && Sites.All(s => s.Secured),
        ObjectiveKind.Ambush => ColumnsSpent && AmbushShare >= ZoneBattle.AmbushQuota,
        ObjectiveKind.Escort => ConvoyMoving && Trucks.Where(t => t.Alive).Count() == Delivered && Delivered >= ZoneBattle.Convoy!.MustArrive,
        _ => Sites.Count > 0 && Sites.All(s => s.Secured),
    };

    /// <summary>Friendly vehicles lost in this battle.</summary>
    public int Losses => Vehicles.Count(v => v.Player && !v.Alive);

    /// <summary>Why the mission has failed although vehicles survive, or null while every condition holds.</summary>
    public string? BrokenCondition() => Conditions switch
    {
        { MaxLosses: int most } when Losses > most => $"More than {most} vehicles lost",
        { TimeLimitSeconds: float limit } when Elapsed > limit => $"Out of time: the objectives had to be secured within {limit / 60:0} minutes",
        { SpareCivilians: true } when Fines > 0 => "A civilian building was destroyed",
        _ when ZoneBattle is { Convoy: EscortConvoy convoy } && Trucks.Count(t => t.Alive) < convoy.MustArrive =>
            $"Too many supply trucks lost: {convoy.MustArrive} had to get through",
        _ when ZoneBattle is { Objective: ObjectiveKind.Ambush } battle && ColumnsSpent && AmbushShare < battle.AmbushQuota =>
            $"Too much of the convoy got away: {ColumnKills} of {battle.ColumnVehicles} destroyed",
        _ => null,
    };

    /// <summary>A Classic night battle: the enemy sees less far (see <see cref="ClassicRules.NightEnemySight"/>).</summary>
    public bool Night => Mission.Night(Campaign);
    public float Elapsed { get; set; }
    public Queue<CombatEvent> Events { get; } = [];
    public Navigation Navigation { get; }
    public List<Obstacle> Obstacles { get; }
    public bool Paused { get; set; }
    public MissionPhase Phase { get; set; } = MissionPhase.Briefing;
    public Reconnaissance Recon { get; }
    public List<Shell> Shells { get; } = [];
    public int ShotsFired { get; private set; }
    public List<CaptureSite> Sites { get; }

    /// <summary>The Classic battle being fought, or null in Modern mode.</summary>
    public ZoneBattle? ZoneBattle { get; }

    /// <summary>Classic ground types; null in Modern mode, where all ground is alike.</summary>
    public TerrainMap? Terrain { get; }

    public List<Vehicle> Vehicles { get; }

    public Simulation NextIsland() => new(Campaign.AfterVictory(this));

    public Simulation RetryIsland() => new(Campaign);

    public Vehicle? Leader(int group) => Group(group).FirstOrDefault();

    public IEnumerable<Vehicle> Group(int group) => Vehicles.Where(v => v.Alive && !v.Enemy && v.Group == group);

    public void Begin()
    {
        if (Phase == MissionPhase.Briefing && Vehicles.Any(v => v.Alive && v.Player))
        {
            Phase = MissionPhase.Active;
            Recon.Refresh();
        }
    }

    /// <summary>Moves a group to <paramref name="point"/> in a 2 × 2 formation. Returns how many vehicles accepted.</summary>
    public int Order(int group, Vector2 point, bool append = false)
    {
        if (!CanOrder || !Navigation.Inside(point))
        {
            return 0;
        }

        int count = 0;
        int index = 0;
        foreach (var tank in Group(group))
        {
            var destination = point + FormationOffset(index);
            index++;
            if (append && tank.Orders.Count >= Vehicle.MaxOrders)
            {
                continue;
            }

            var from = append && tank.Orders.Count > 0 ? tank.Orders[^1] : tank.Position;
            var route = Navigation.FindPath(from, destination);
            if (route.Count == 0)
            {
                continue;
            }

            if (!append)
            {
                tank.Orders.Clear();
                tank.Route.Clear();
            }
            tank.Orders.Add(route[^1]);
            if (tank.Route.Count == 0)
            {
                if (from == tank.Position)
                {
                    tank.Route = route;
                }
                else
                {
                    // Orders were queued but nothing is in progress: plan from where the tank actually is.
                    RouteToNextOrder(tank);
                }
            }

            tank.Status = VehicleStatus.Moving;
            count++;
        }
        Events.Enqueue(new(CombatEventKind.Order, point, count == 0 ? "No reachable route" : "Movement order confirmed"));
        return count;
    }

    public void Stop(int group)
    {
        if (!CanOrder)
        {
            return;
        }

        foreach (var tank in Group(group))
        {
            tank.Orders.Clear();
            tank.Route.Clear();
            tank.Speed = 0;
            tank.Status = VehicleStatus.Holding;
        }
    }

    public void Regroup(int group)
    {
        var leader = Leader(group);
        if (!CanOrder || leader == null)
        {
            return;
        }

        var back = -Geometry.Forward(leader.Heading);
        var right = new Vector2(MathF.Cos(leader.Heading), -MathF.Sin(leader.Heading));
        int slot = 0;
        int moving = 0;
        foreach (var tank in Group(group).Where(v => v != leader))
        {
            var destination = leader.Position +
                back * (RegroupFirstRowDistance + (slot / 2) * RegroupRowSpacing) +
                right * ((slot % 2) * RegroupColumnSpacing - RegroupColumnSpacing / 2);
            slot++;
            var path = Navigation.FindPath(tank.Position, destination);
            if (path.Count == 0)
            {
                continue;
            }

            tank.Route = path;
            tank.Orders.Clear();
            tank.Orders.Add(path[^1]);
            tank.Status = VehicleStatus.Moving;
            moving++;
        }
        Events.Enqueue(new(CombatEventKind.Order, leader.Position, moving == 0 ? "No room to regroup" : "Group reforming behind lead vehicle"));
    }

    public void ToggleFire(int group)
    {
        if (!CanOrder)
        {
            return;
        }

        // A Classic group still waiting for contact goes weapons free.
        bool hold = !Group(group).Any(v => v.Waiting) && !Group(group).All(v => v.HoldFire);
        foreach (var tank in Group(group))
        {
            tank.HoldFire = hold;
            tank.Waiting = false;
        }
    }

    /// <summary>
    /// Advances the battle. <paramref name="manualId"/> is the vehicle under direct control, driven
    /// by <paramref name="drive"/> (−1 reverse … 1 forward) and <paramref name="turn"/> (−1 … 1).
    /// </summary>
    public void Step(float dt, int manualId = -1, float drive = 0, float turn = 0)
    {
        if (Phase != MissionPhase.Active || Paused || dt <= 0)
        {
            return;
        }

        float frame = Math.Min(dt, MaxFrameSeconds);
        int steps = (int)MathF.Ceiling(frame / SubstepSeconds);
        float slice = frame / steps;
        for (int i = 0; i < steps && Phase == MissionPhase.Active; i++)
        {
            Tick(slice, manualId, drive, turn);
        }
    }

    /// <summary>
    /// Plans a route to the vehicle's next queued order, dropping orders that have become
    /// unreachable so the vehicle never sits with orders but no route.
    /// </summary>
    internal void RouteToNextOrder(Vehicle tank)
    {
        while (tank.Orders.Count > 0)
        {
            tank.Route = Navigation.FindPath(tank.Position, tank.Orders[0]);
            if (tank.Route.Count > 0)
            {
                tank.Status = VehicleStatus.Moving;
                return;
            }

            tank.Orders.RemoveAt(0);
            tank.Status = VehicleStatus.NoRoute;
        }
    }

    private static Vector2 FormationOffset(int index) => new(
        (index % 2) * FormationSpacing - FormationSpacing / 2,
        (index / 2) * FormationSpacing - FormationSpacing / 2);

    private static float Approach(float value, float target, float step) => value + Math.Clamp(target - value, -step, step);

    private void Tick(float dt, int manualId, float drive, float turn)
    {
        Elapsed += dt;
        Recon.Tick(dt);
        foreach (var tank in Vehicles.Where(v => v.Alive))
        {
            tank.Reload = Math.Max(0, tank.Reload - dt);
            tank.Revealed = Math.Max(0, tank.Revealed - dt);
            tank.Think -= dt;
            if (tank.Enemy && tank.Think <= 0)
            {
                Think(tank);
            }

            bool manual = tank.Id == manualId && (drive != 0 || turn != 0);
            if (manual)
            {
                tank.Route.Clear();
                tank.Orders.Clear();
                tank.Heading += turn * tank.Spec.HullTurn * dt;
                tank.Speed = Approach(tank.Speed, drive * TopSpeed(tank), dt * ManualAcceleration);
                Move(tank, Geometry.Forward(tank.Heading) * tank.Speed * dt);
                tank.Status = VehicleStatus.DirectControl;
            }
            else
            {
                FollowRoute(tank, dt);
            }

            AimAndFire(tank, dt);
        }

        UpdateShells(dt);
        UpdateSupport(dt);
        UpdateColumns();
        UpdateConvoy(dt);
        UpdateFireDiscipline();
        UpdateCapture(dt);
        Command(dt);
        Resupply(dt);
        if (!Vehicles.Any(v => v.Alive && v.Player))
        {
            Phase = MissionPhase.Defeat;
        }
        else if (BrokenCondition() is string reason)
        {
            Phase = MissionPhase.Defeat;
            Events.Enqueue(new(CombatEventKind.Order, Vector2.Zero, $"Mission failed: {reason}"));
        }
        else if (ObjectiveMet())
        {
            Phase = MissionPhase.Victory;
        }
    }

    private void UpdateCapture(float dt)
    {
        foreach (var site in Sites)
        {
            bool friends = Vehicles.Any(v => v.Alive && !v.Enemy && Vector2.Distance(v.Position, site.Position) < CaptureSite.FriendlyRadius);
            bool enemies = Vehicles.Any(v => v.Alive && v.Enemy && Vector2.Distance(v.Position, site.Position) < CaptureSite.EnemyRadius);
            bool wasSecured = site.Secured;
            site.Contested = friends && enemies;
            if (friends && !enemies)
            {
                site.Progress = Math.Min(CaptureSite.HoldSeconds, site.Progress + dt);
            }
            else if (enemies)
            {
                site.Progress = Math.Max(0, site.Progress - dt * CaptureSite.DecayRate);
            }

            site.SecuredFor = site.Secured ? site.SecuredFor + dt : 0;
            if (!wasSecured && site.Secured)
            {
                Events.Enqueue(new(CombatEventKind.Capture, site.Position, $"{site.Name} secured"));
            }
            else if (wasSecured && !site.Secured)
            {
                Events.Enqueue(new(CombatEventKind.SiteLost, site.Position, $"{site.Name} under enemy pressure: retake it"));
            }
        }
    }

    // The southern beach is the supply zone. Vehicles repair and rearm only while stationary and out of contact.
    private void Resupply(float dt)
    {
        bool rearmTick = (int)(Elapsed * RearmPerSecond) != (int)((Elapsed - dt) * RearmPerSecond);
        foreach (var tank in Vehicles.Where(v => v.Alive && v.Player && v.Position.Y > Mission.SupplyLineY && Math.Abs(v.Speed) < StationarySpeed))
        {
            if (Target(tank) != null)
            {
                continue;
            }

            tank.Health = Math.Min(Vehicle.MaxHealth, tank.Health + dt * RepairPerSecond);
            if (rearmTick)
            {
                tank.Ammo = Math.Min(tank.Spec.Ammo, tank.Ammo + 1);
                for (int w = 0; w < tank.Rounds.Length; w++)
                {
                    tank.Rounds[w] = Math.Min(tank.Spec.Magazine[w], tank.Rounds[w] + 1);
                }
            }
        }
    }

    private void FollowRoute(Vehicle tank, float dt)
    {
        if (tank.Route.Count == 0)
        {
            tank.Speed = 0;
            if (tank.Orders.Count > 0)
            {
                RouteToNextOrder(tank);
            }
            else if (tank.Status != VehicleStatus.NoRoute)
            {
                tank.Status = VehicleStatus.Holding;
            }

            return;
        }

        var delta = tank.Route[0] - tank.Position;
        float distance = delta.Length();
        if (distance < WaypointReached)
        {
            tank.Route.RemoveAt(0);
            if (tank.Route.Count == 0 && tank.Orders.Count > 0)
            {
                tank.Orders.RemoveAt(0);
                RouteToNextOrder(tank);
            }
            return;
        }

        float angle = Geometry.Angle(delta);
        tank.Heading = Geometry.Turn(tank.Heading, angle, tank.Spec.HullTurn * dt);
        float offHeading = Geometry.Delta(tank.Heading, angle);
        float speed = Math.Abs(offHeading) > MaxDriveAngle ? 0 : TopSpeed(tank) * Math.Max(0, MathF.Cos(offHeading));
        tank.Speed = Approach(tank.Speed, speed, dt * RouteAcceleration);
        var before = tank.Position;
        float attemptedSpeed = tank.Speed;
        Move(tank, Geometry.Forward(tank.Heading) * Math.Min(distance, tank.Speed * dt));
        bool blocked = attemptedSpeed > MovingThreshold && Vector2.Distance(before, tank.Position) < dt * MinimumProgress;
        tank.Stuck = blocked ? tank.Stuck + dt : Math.Max(0, tank.Stuck - dt);
        tank.Status = blocked ? VehicleStatus.Yielding : VehicleStatus.Moving;
        if (tank.Stuck > StuckReplanSeconds + tank.Id % 4 * StuckReplanStagger)
        {
            var goal = tank.Orders.Count > 0 ? tank.Orders[0] : tank.Route[^1];
            var route = Navigation.FindPath(tank.Position, goal, Vehicles, tank.Id);
            if (route.Count > 0)
            {
                tank.Route = route;
            }

            tank.Stuck = 0;
        }
    }

    /// <summary>The fastest this vehicle can go where it stands: Classic terrain slows it.</summary>
    private float TopSpeed(Vehicle tank) =>
        Terrain == null ? tank.Spec.Speed : tank.Spec.Speed * TerrainMap.Speed(Terrain.At(tank.Position));

    private void Move(Vehicle tank, Vector2 delta)
    {
        var end = tank.Position + delta;
        // The endpoint is checked too: a sweep almost parallel to a wall tests only its start, so tiny
        // sideways steps could otherwise creep into the wall.
        bool clear = Navigation.Clear(tank.Position, end, Mission.VehicleRadius) && Navigation.Clear(end, end, Mission.VehicleRadius) &&
            !Vehicles.Any(v => v != tank && Geometry.HitCircle(tank.Position, end, v.Position, Mission.VehicleRadius * 2).HasValue);
        if (clear)
        {
            tank.Position = end;
        }
        else
        {
            tank.Speed = 0;
        }
    }
}
