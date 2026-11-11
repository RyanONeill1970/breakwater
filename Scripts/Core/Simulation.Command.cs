namespace Breakwater.Core;

using System.Numerics;

// Enemy command. Individual defenders fight around their posts (Simulation.Ai.cs); the command
// ties the garrisons together: it sends idle defenders to relieve a site under attack or retake a
// secured one, and lands reinforcement waves on the north shore. Every order it gives goes out
// as a radio call the player intercepts, so the player always gets warning.
public sealed partial class Simulation
{
    /// <summary>The command reviews the island at this interval rather than every substep.</summary>
    private const float CommandSeconds = 1;

    /// <summary>After sending a force, the command waits this long before sending another.</summary>
    public const float DispatchCooldown = 40;

    /// <summary>A site must stay secured this long before the enemy counter-attacks it.</summary>
    public const float CounterAttackDelay = 20;

    /// <summary>Defenders sent to retake a secured site.</summary>
    public const int CounterAttackSize = 3;

    /// <summary>Defenders sent to relieve a site the player is capturing.</summary>
    public const int ReliefSize = 2;

    /// <summary>The command never draws a garrison below this many defenders.</summary>
    public const int GarrisonMinimum = 2;

    // Only healthy, armed defenders are sent away from their posts.
    private const float DispatchMinHealth = 60;
    private const int DispatchMinAmmo = 8;

    /// <summary>Seconds into the battle before the first reinforcement wave lands.</summary>
    public const float FirstWaveSeconds = 70;

    /// <summary>Seconds between reinforcement waves.</summary>
    public const float WaveIntervalSeconds = 70;

    // Landing craft put vehicles ashore in a line; if the beach is crowded they search outwards.
    private const float LandingSpacing = 7;
    private const float LandingSearchStep = 5;
    private const int LandingSearchRings = 5;
    private const float LandingClearance = .5f;
    private const float LandingThinkStagger = .4f;

    // Reinforcements for a garrison take posts in a row behind the original defenders.
    private static readonly Vector2 ReinforcementPostOffset = new(-7, -19);
    private const float ReinforcementPostSpacing = 7;

    /// <summary>Assaulting defenders spread around a site at this radius, inside <see cref="CaptureSite.EnemyRadius"/>.</summary>
    private const float AssaultSpread = 8;

    private float untilCommand;

    /// <summary>Elapsed time before which the command will not send another force.</summary>
    public float NextDispatch { get; set; }

    /// <summary>Reinforcement waves the enemy calls in during this battle.</summary>
    public int ReinforcementWaves { get; set; }

    /// <summary>Reinforcement waves that have landed so far.</summary>
    public int WavesLanded { get; set; }

    /// <summary>Elapsed time at which the next reinforcement wave lands, or null when none remain.</summary>
    public float? NextWave => WavesLanded < ReinforcementWaves ? FirstWaveSeconds + WavesLanded * WaveIntervalSeconds : null;

    /// <summary>
    /// Whether an enemy force is on its way to <paramref name="site"/>. The player learns this from the
    /// intercepted order, so the HUD may show it even when the force itself is hidden.
    /// </summary>
    public bool Threatened(int site) => Vehicles.Any(v => v.Alive && v.Enemy && v.Objective == site);

    private void Command(float dt)
    {
        // Classic defenders hold their ground: there is no command to send them anywhere.
        if (Classic)
        {
            return;
        }

        untilCommand -= dt;
        if (untilCommand > 0)
        {
            return;
        }

        untilCommand = CommandSeconds;
        if (Elapsed >= NextWave)
        {
            LandReinforcements();
        }

        Dispatch();
    }

    /// <summary>Sends one force at a time: a counter-attack on a secured site, or relief for one being captured.</summary>
    private void Dispatch()
    {
        if (Elapsed < NextDispatch || Vehicles.Any(v => v.Alive && v.Enemy && v.Objective != Vehicle.NoObjective))
        {
            return;
        }

        var counterAttack = Sites.FindIndex(s => s.Secured && s.SecuredFor >= CounterAttackDelay);
        var relief = Sites.FindIndex(s => !s.Secured && s.Progress > 0);
        int target = counterAttack >= 0 ? counterAttack : relief;
        if (target < 0)
        {
            return;
        }

        var site = Sites[target];
        var garrisons = Vehicles
            .Where(v => v.Alive && v.Enemy && v.Objective == Vehicle.NoObjective)
            .GroupBy(GarrisonOf)
            .ToDictionary(g => g.Key, g => g.Count());
        List<Vehicle> force = [];
        int size = counterAttack >= 0 ? CounterAttackSize : ReliefSize;
        foreach (var tank in Vehicles
            .Where(v => v.Alive && v.Enemy && v.Objective == Vehicle.NoObjective && v.Tactic is Tactic.Guard or Tactic.Patrol &&
                v.Health >= DispatchMinHealth && v.RoundsLeft >= DispatchMinAmmo && GarrisonOf(v) != target)
            .OrderBy(v => Vector2.DistanceSquared(v.Position, site.Position)))
        {
            int garrison = GarrisonOf(tank);
            if (force.Count == size || garrisons[garrison] <= GarrisonMinimum)
            {
                continue;
            }

            garrisons[garrison]--;
            force.Add(tank);
        }

        if (force.Count == 0)
        {
            return;
        }

        foreach (var tank in force)
        {
            Assign(tank, target);
        }

        NextDispatch = Elapsed + DispatchCooldown;
        string message = counterAttack >= 0
            ? $"Intercept: enemy counter-attack on {site.Name}"
            : $"Intercept: enemy moving to relieve {site.Name}";
        Events.Enqueue(new(CombatEventKind.Assault, site.Position, message));
    }

    /// <summary>
    /// Lands a wave on the north shore. If the player holds a site, the wave counter-attacks it
    /// straight off the beach; otherwise it reinforces the weakest garrison.
    /// </summary>
    private void LandReinforcements()
    {
        var beach = Mission.LandingPoint(Campaign.Island);
        bool assaultUnderway = Vehicles.Any(v => v.Alive && v.Enemy && v.Objective != Vehicle.NoObjective);
        int target = assaultUnderway ? -1 : Sites.FindIndex(s => s.Secured);
        int garrison = target >= 0 ? -1 : Enumerable.Range(0, Sites.Count)
            .Where(i => !Sites[i].Secured)
            .OrderBy(i => Vehicles.Count(v => v.Alive && v.Enemy && v.Objective == Vehicle.NoObjective && GarrisonOf(v) == i))
            .First();
        int landed = 0;
        for (int n = 0; n < Mission.WaveSize; n++)
        {
            var spot = OpenGround(beach + new Vector2((n - (Mission.WaveSize - 1) / 2f) * LandingSpacing, 0));
            if (spot == null)
            {
                continue;
            }

            var position = spot.Value;
            var tank = new Vehicle
            {
                Id = Mission.FirstReinforcementId + Vehicles.Count(v => v.Id >= Mission.FirstReinforcementId),
                Enemy = true,
                Group = Mission.EnemyGroup,
                // Coming ashore from the north, facing the island.
                Heading = MathF.PI,
                Home = garrison >= 0 ? Sites[garrison].Position + ReinforcementPostOffset + new Vector2(landed * ReinforcementPostSpacing, 0) : position,
                Kind = VehicleKind.Defender,
                Position = position,
                Think = n * LandingThinkStagger,
                TurretHeading = MathF.PI,
            };
            tank.Rearm();
            if (target >= 0)
            {
                Assign(tank, target);
            }

            Vehicles.Add(tank);
            landed++;
        }

        WavesLanded++;
        if (landed == 0)
        {
            return;
        }

        string message = target >= 0
            ? $"Intercept: enemy landing in the north, heading for {Sites[target].Name}"
            : $"Intercept: enemy reinforcements landing in the north";
        Events.Enqueue(new(CombatEventKind.Reinforcement, beach, message));
    }

    private void Assign(Vehicle tank, int site)
    {
        tank.Objective = site;
        tank.Tactic = Tactic.Assault;
        tank.Think = Math.Min(tank.Think, 0);
    }

    /// <summary>The site whose garrison a defender belongs to: the one nearest its post.</summary>
    private int GarrisonOf(Vehicle tank)
    {
        int best = 0;
        for (int i = 1; i < Sites.Count; i++)
        {
            if (Vector2.DistanceSquared(Sites[i].Position, tank.Home) < Vector2.DistanceSquared(Sites[best].Position, tank.Home))
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>Where an assaulting defender stands at its objective, spread by role so they surround it.</summary>
    private static Vector2 AssaultPoint(CaptureSite site, int role)
    {
        float angle = role * MathF.Tau / 4;
        return site.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * AssaultSpread;
    }

    /// <summary>The nearest spot to <paramref name="near"/> where a hull fits clear of cover and other vehicles.</summary>
    private Vector2? OpenGround(Vector2 near)
    {
        for (int ring = 0; ring <= LandingSearchRings; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                    {
                        continue;
                    }

                    var p = near + new Vector2(dx, dy) * LandingSearchStep;
                    if (Navigation.Inside(p) &&
                        Navigation.Clear(p, p, Mission.VehicleRadius + LandingClearance) &&
                        !Vehicles.Any(v => Vector2.Distance(v.Position, p) < Mission.VehicleRadius * 2 + LandingClearance))
                    {
                        return p;
                    }
                }
            }
        }

        return null;
    }
}
