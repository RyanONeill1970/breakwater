namespace Breakwater.Core;

using System.Numerics;

// Classic support weapons: smoke dischargers, artillery missions and mines. Smoke blocks sight for
// both sides; artillery arrives after a delay as a scattered volley that hurts whoever is under it;
// mines hurt whoever drives over them. The enemy has its own guns and minefields.
public sealed partial class Simulation
{
    // Smoke: thrown ahead of the vehicle, about 200 m across, clearing after 40 seconds.
    public static readonly float SmokeThrow = ClassicRules.Distance(150);
    public static readonly float SmokeRadius = ClassicRules.Distance(200);
    public const float SmokeSeconds = 40;

    /// <summary>Artillery missions each side may call in one battle.</summary>
    public const int PlayerArtilleryMissions = 3;

    /// <summary>Seconds from a call to the first round landing, and between rounds.</summary>
    public const float ArtilleryDelay = 12;
    private const float ArtilleryRoundInterval = .45f;
    public const int ArtilleryRounds = 6;

    /// <summary>Rounds fall around the target with this standard deviation, in world metres.</summary>
    private static readonly float ArtilleryScatter = ClassicRules.Distance(160);

    // Blast power (as ammunition power against armour) and radius of each shell type.
    public const float HighExplosivePower = 500;
    public const float ClusterPower = 190;
    public static readonly float HighExplosiveRadius = ClassicRules.Distance(120);
    public static readonly float ClusterRadius = ClassicRules.Distance(220);
    public static readonly float SmokeShellRadius = ClassicRules.Distance(260);

    // The enemy's guns: a few missions per island, fired at recently reported player vehicles.
    private const int EnemyArtilleryBase = 1;
    private const float EnemyArtilleryFirstSeconds = 45;
    private const float EnemyArtilleryInterval = 60;
    private const float EnemyArtilleryReportAge = 2;

    /// <summary>Mines the player's force can lay in one battle.</summary>
    public const int PlayerMines = 6;

    /// <summary>A mine arms this long after it is laid, so the layer can drive off it.</summary>
    public const float MineArmSeconds = 5;

    /// <summary>A mine's power against armour: enough to wreck a carrier and badly hurt a tank.</summary>
    public const float MinePower = 1000;

    /// <summary>A mine is laid this far behind the vehicle laying it.</summary>
    private static readonly float MineDrop = Mission.VehicleRadius * 2;

    public int ArtilleryLeft { get; set; }
    public int EnemyArtilleryLeft { get; set; }
    public float NextEnemyArtillery { get; set; } = EnemyArtilleryFirstSeconds;
    public List<Mine> Mines { get; } = [];
    public int MinesLeft { get; set; }
    public List<SmokeCloud> Smoke { get; } = [];
    public List<ArtilleryStrike> Strikes { get; } = [];

    /// <summary>Whether smoke hides <paramref name="b"/> from <paramref name="a"/>.</summary>
    public bool Screened(Vector2 a, Vector2 b) =>
        Smoke.Count > 0 && Smoke.Any(c => Geometry.DistanceToSegment(c.Position, a, b) < c.Radius);

    /// <summary>Every vehicle in <paramref name="group"/> with smoke left fires one ahead of itself. Returns how many did.</summary>
    public int FireSmoke(int group)
    {
        if (!CanOrder || !Classic)
        {
            return 0;
        }

        int fired = 0;
        foreach (var tank in Group(group).Where(v => v.Smoke > 0))
        {
            tank.Smoke--;
            AddSmoke(tank.Position + Geometry.Forward(tank.TurretHeading) * SmokeThrow, SmokeRadius);
            fired++;
        }

        Events.Enqueue(new(CombatEventKind.Order, Leader(group)?.Position ?? Vector2.Zero, fired == 0 ? "No smoke left" : "Smoke out"));
        return fired;
    }

    /// <summary>Calls an artillery mission on <paramref name="target"/>. False when none are left.</summary>
    public bool CallArtillery(Vector2 target, ArtilleryShell shell, bool enemy = false)
    {
        if (!CanOrder || !Classic || !Navigation.Inside(target) || (enemy ? EnemyArtilleryLeft : ArtilleryLeft) <= 0)
        {
            return false;
        }

        if (enemy)
        {
            EnemyArtilleryLeft--;
        }
        else
        {
            ArtilleryLeft--;
        }

        Strikes.Add(new ArtilleryStrike { Enemy = enemy, NextRound = Elapsed + ArtilleryDelay, RoundsLeft = ArtilleryRounds, Shell = shell, Target = target });
        string message = enemy ? "Incoming artillery: clear the area!" : $"Fire mission: {ShellName(shell)}, {ArtilleryDelay:0} seconds";
        Events.Enqueue(new(enemy ? CombatEventKind.IncomingArtillery : CombatEventKind.ArtilleryCalled, target, message));
        return true;
    }

    /// <summary>The leader of <paramref name="group"/> drops a mine behind it. False when none are left.</summary>
    public bool LayMine(int group)
    {
        var leader = Leader(group);
        if (!CanOrder || !Classic || leader == null || MinesLeft <= 0)
        {
            return false;
        }

        MinesLeft--;
        Mines.Add(new Mine(leader.Position - Geometry.Forward(leader.Heading) * MineDrop, false, Elapsed + MineArmSeconds));
        Events.Enqueue(new(CombatEventKind.Order, leader.Position, $"Mine laid. {MinesLeft} left"));
        return true;
    }

    public static string ShellName(ArtilleryShell shell) => shell switch
    {
        ArtilleryShell.Cluster => "cluster",
        ArtilleryShell.Smoke => "smoke",
        _ => "high explosive",
    };

    /// <summary>Arms both sides for a Classic battle: missions, mines and the enemy's minefields.</summary>
    private void SetUpSupport()
    {
        if (!Classic)
        {
            return;
        }

        ArtilleryLeft = PlayerArtilleryMissions;
        MinesLeft = PlayerMines;
        if (ZoneBattle is { Training: true })
        {
            return;
        }

        EnemyArtilleryLeft = EnemyArtilleryBase + Campaign.Island;
        if (ZoneBattle is { EnemyMines: false })
        {
            return;
        }

        Mines.AddRange(Mission.Minefield(Campaign.Island).Select(p => new Mine(p, true, 0)));
    }

    private void UpdateSupport(float dt)
    {
        if (!Classic)
        {
            return;
        }

        Smoke.RemoveAll(c => Elapsed >= c.Until);
        foreach (var strike in Strikes.Where(s => Elapsed >= s.NextRound).ToList())
        {
            LandRound(strike);
            strike.RoundsLeft--;
            strike.NextRound = Elapsed + ArtilleryRoundInterval;
        }

        Strikes.RemoveAll(s => s.RoundsLeft <= 0);
        CheckMines();
        if (Elapsed >= NextEnemyArtillery)
        {
            NextEnemyArtillery = Elapsed + EnemyArtilleryInterval;
            CallEnemyArtillery();
        }
    }

    private void LandRound(ArtilleryStrike strike)
    {
        var at = strike.Target + new Vector2(Gaussian(), Gaussian()) * ArtilleryScatter;
        if (!Navigation.Inside(at))
        {
            return;
        }

        bool visible = !strike.Enemy || Recon.PointVisible(at) || Vehicles.Any(v => v.Alive && !v.Enemy && Vector2.Distance(v.Position, at) < ClusterRadius);
        Events.Enqueue(new(CombatEventKind.Barrage, at, PlayerVisible: visible));
        switch (strike.Shell)
        {
            case ArtilleryShell.Smoke:
                AddSmoke(at, SmokeShellRadius);
                break;
            case ArtilleryShell.Cluster:
                Blast(at, ClusterPower, ClusterRadius);
                BlastBuildings(at, ClusterPower, ClusterRadius, !strike.Enemy);
                break;
            default:
                Blast(at, HighExplosivePower, HighExplosiveRadius);
                BlastBuildings(at, HighExplosivePower, HighExplosiveRadius, !strike.Enemy);
                break;
        }
    }

    /// <summary>Damages every vehicle within <paramref name="radius"/>, falling off with distance.</summary>
    private void Blast(Vector2 at, float power, float radius)
    {
        foreach (var victim in Vehicles.Where(v => v.Alive && Vector2.Distance(v.Position, at) < radius))
        {
            float falloff = 1 - Vector2.Distance(victim.Position, at) / radius;
            Harm(victim, ClassicDamage(victim, power) * falloff, at);
        }
    }

    private void CheckMines()
    {
        for (int i = Mines.Count - 1; i >= 0; i--)
        {
            var mine = Mines[i];
            var victim = Elapsed < mine.ArmedAt ? null : Vehicles.FirstOrDefault(v => v.Alive && Vector2.Distance(v.Position, mine.Position) < Mission.VehicleRadius);
            if (victim == null)
            {
                continue;
            }

            Mines.RemoveAt(i);
            Events.Enqueue(new(CombatEventKind.MineBlast, mine.Position,
                victim.Enemy ? "" : $"{Mission.UnitName(victim)} hit a mine",
                victim.Id, !victim.Enemy || Recon.PointVisible(mine.Position)));
            Harm(victim, ClassicDamage(victim, MinePower), mine.Position);
        }
    }

    /// <summary>The enemy fires a mission at a player vehicle one of its units has just reported, once the fighting has started.</summary>
    private void CallEnemyArtillery()
    {
        if (Vehicles.All(v => !v.Enemy || v.Waiting || !v.Alive))
        {
            return;
        }

        var report = Recon.EnemyIntel.Values
            .Where(c => !c.Destroyed && Elapsed - c.LastSeen <= EnemyArtilleryReportAge)
            .OrderBy(c => c.Id)
            .FirstOrDefault();
        if (report != null)
        {
            CallArtillery(report.Position, ArtilleryShell.HighExplosive, enemy: true);
        }
    }

    private void AddSmoke(Vector2 at, float radius)
    {
        Smoke.Add(new SmokeCloud(at, radius, Elapsed + SmokeSeconds));
        Events.Enqueue(new(CombatEventKind.Smoke, at, PlayerVisible: true));
        Recon.Refresh();
    }

    /// <summary>Takes <paramref name="damage"/> off <paramref name="victim"/>, wrecking it below the Classic threshold, and reports a kill.</summary>
    private void Harm(Vehicle victim, float damage, Vector2 at)
    {
        victim.Health = Math.Max(0, victim.Health - damage);
        if (victim.Health < ClassicRules.DestroyedBelow)
        {
            victim.Health = 0;
        }

        if (victim.Alive)
        {
            return;
        }

        Destroyed(victim);
        victim.Route.Clear();
        victim.Orders.Clear();
        victim.Speed = 0;
        string message = victim.Enemy ? "" : $"{Mission.UnitName(victim)} vehicle lost";
        Events.Enqueue(new(CombatEventKind.Destroy, victim.Position, message, victim.Id, !victim.Enemy || Recon.PointVisible(at)));
    }
}
