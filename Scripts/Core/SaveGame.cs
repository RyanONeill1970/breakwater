namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// Versioned checkpoints. The file format is the private DTOs below, not the live simulation
/// types, so adding a runtime field to <see cref="Vehicle"/> cannot silently change or break saves.
/// </summary>
/// <remarks>
/// Version 1: single mission, no campaign. Version 2: adds <c>Campaign</c> and <c>Phase</c>.
/// Version 3: adds <c>Recon</c>. Version 4: adds the enemy command (<c>NextDispatch</c>,
/// <c>SecuredFor</c>, <c>WavesLanded</c>), landed reinforcements after the deployed vehicles, and each
/// vehicle's <c>Objective</c> and moved <c>Home</c>. Version 5: adds Classic mode, the campaign's
/// <c>Mode</c> and each Classic vehicle's <c>Rounds</c>, <c>Loaded</c> and <c>Revealed</c>. Version 6: adds Classic support weapons: missions
/// and mines left, the enemy's next fire mission, mines on the ground, smoke clouds, missions in flight
/// and each vehicle's <c>Smoke</c>. Version 7: adds the Classic economy: <c>BuildingHealth</c> for the
/// island's original layout, and the battle's <c>Bounty</c> and <c>Fines</c>. Version 8: adds the
/// Classic zone campaign's <c>Campaign.Battle</c>; an older Classic save resumes at its island's final
/// battle, which is the battle it was. Version 9: adds Classic enemy columns: arrivals are saved after
/// the deployed vehicles with their own ids and kinds, and <c>Escaped</c> counts convoy vehicles that
/// left the island, and each vehicle's <c>Waiting</c> (garrison fire discipline). Older vehicle
/// entries carried extra runtime fields (route, speed, status...), which are ignored on read.
/// </remarks>
public static class SaveGame
{
    public const int CurrentVersion = 9;

    /// <summary>Slack allowed when comparing saved timestamps, for float rounding.</summary>
    public const float TimeTolerance = .01f;

    /// <summary>Metres two saved hulls may overlap by, for float rounding.</summary>
    private const float OverlapTolerance = .01f;

    // IncludeFields is needed for System.Numerics.Vector2, whose X/Y are public fields.
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true, WriteIndented = true };

    // Shells are intentionally excluded: quicksave is only allowed when no rounds are in flight.
    public static string Write(Simulation sim)
    {
        if (sim.Phase == MissionPhase.Active && sim.Shells.Count != 0)
        {
            throw new InvalidOperationException("Wait for shells to land before saving.");
        }

        var deployed = Mission.Vehicles(sim.Campaign).ToDictionary(v => v.Id);
        var snapshot = new Snapshot
        {
            ArtilleryLeft = sim.ArtilleryLeft,
            Bounty = sim.Bounty,
            BuildingHealth = [.. sim.BuildingHealth],
            Campaign = sim.Campaign,
            Capture = sim.Sites.Select(s => s.Progress).ToArray(),
            EnemyArtilleryLeft = sim.EnemyArtilleryLeft,
            Escaped = sim.Escaped,
            Fines = sim.Fines,
            Mines = [.. sim.Mines],
            MinesLeft = sim.MinesLeft,
            NextEnemyArtillery = sim.NextEnemyArtillery,
            Smoke = [.. sim.Smoke],
            Strikes = [.. sim.Strikes],
            Elapsed = sim.Elapsed,
            NextDispatch = sim.NextDispatch,
            Phase = sim.Phase,
            Recon = sim.Recon.Snapshot(),
            SecuredFor = sim.Sites.Select(s => s.SecuredFor).ToArray(),
            Vehicles = sim.Vehicles.Select(v => VehicleSave.From(v, deployed.GetValueOrDefault(v.Id))).ToArray(),
            WavesLanded = sim.WavesLanded,
        };
        return JsonSerializer.Serialize(snapshot, Options);
    }

    public static Simulation Read(string json)
    {
        var data = JsonSerializer.Deserialize<Snapshot>(json, Options) ?? throw new FormatException("Empty save.");
        if (data.Version is < 1 or > CurrentVersion || (data.Version >= 2 && (data.Campaign == null || !data.Campaign.Valid())) || !Enum.IsDefined(data.Phase))
        {
            throw new FormatException("Unsupported or invalid campaign save.");
        }

        if (data.Version < 8 && data.Campaign is { Mode: GameMode.Classic } legacy)
        {
            legacy.Battle = ClassicCampaign.FinalBattle(legacy.Island);
        }

        var sim = new Simulation(data.Version == 1 ? null : data.Campaign);
        if (data.Version >= 4)
        {
            RestoreCommand(data, sim);
        }

        // Before the vehicles, which may stand where a building has been knocked down.
        if (data.Version >= 7)
        {
            RestoreEconomy(data, sim);
        }

        if (data.Vehicles == null || data.Capture == null || data.Vehicles.Length != sim.Vehicles.Count || data.Capture.Length != sim.Sites.Count ||
            !float.IsFinite(data.Elapsed) || data.Elapsed < 0 || data.Elapsed > Campaign.MaxRecordedSeconds ||
            !data.Capture.All(c => float.IsFinite(c) && c >= 0 && c <= CaptureSite.HoldSeconds))
        {
            throw new FormatException("Unsupported or invalid save.");
        }

        for (int i = 0; i < data.Vehicles.Length; i++)
        {
            var saved = data.Vehicles[i];
            var vehicle = sim.Vehicles[i];
            if (saved == null || !saved.IsValidFor(vehicle, sim, data.Version))
            {
                throw new FormatException("Invalid vehicle data.");
            }

            if (data.Vehicles.Take(i).Any(other => Vector2.Distance(other.Position, saved.Position) < Mission.VehicleRadius * 2 - OverlapTolerance))
            {
                throw new FormatException("Overlapping vehicles in save.");
            }

            saved.ApplyTo(vehicle, data.Version);
            if (vehicle.Alive && vehicle.Orders.Count > 0)
            {
                sim.RouteToNextOrder(vehicle);
            }
        }

        for (int i = 0; i < sim.Sites.Count; i++)
        {
            sim.Sites[i].Progress = data.Capture[i];
        }

        // Set before the outcome check, which may depend on a time limit.
        sim.Elapsed = data.Elapsed;
        bool allCaptured = sim.ObjectiveMet();
        bool anyFriendly = sim.Vehicles.Any(v => v.Alive && v.Player);
        var phase = data.Version == 1 ? MissionPhase.Active : data.Phase;
        if ((phase == MissionPhase.Victory && (!allCaptured || !anyFriendly)) ||
            (phase == MissionPhase.Defeat && anyFriendly && sim.BrokenCondition() == null) ||
            (phase == MissionPhase.Active && (!anyFriendly || allCaptured)))
        {
            throw new FormatException("Invalid mission outcome.");
        }

        if (phase == MissionPhase.Briefing && (data.Elapsed != 0 || data.Capture.Any(c => c != 0)))
        {
            throw new FormatException("Invalid deployment state.");
        }

        if (data.Version >= 6)
        {
            RestoreSupport(data, sim);
        }

        sim.Phase = phase;
        sim.Paused = phase == MissionPhase.Active;
        if (data.Version >= 3)
        {
            sim.Recon.Restore(data.Recon ?? throw new FormatException("Missing reconnaissance data."));
        }
        else
        {
            sim.Recon.Refresh();
        }

        sim.Events.Clear();
        return sim;
    }

    /// <summary>
    /// Restores the enemy command's timers and adds the reinforcements that had landed, so the vehicle
    /// entries that follow line up with <paramref name="sim"/>'s vehicles.
    /// </summary>
    private static void RestoreCommand(Snapshot data, Simulation sim)
    {
        int deployed = sim.Vehicles.Count;
        int landed = (data.Vehicles?.Length ?? 0) - deployed;
        // Classic columns arrive in fixed sizes, and convoy vehicles that escaped are no longer saved.
        int arrived = data.WavesLanded < 0 || data.WavesLanded > sim.ReinforcementWaves ? 0
            : sim.Classic ? sim.ColumnFirstId(data.WavesLanded) - Mission.FirstReinforcementId : data.WavesLanded * Mission.WaveSize;
        if (data.WavesLanded < 0 || data.WavesLanded > sim.ReinforcementWaves ||
            landed < 0 || landed > arrived || data.Escaped < 0 || landed + data.Escaped > arrived || (!sim.Classic && data.Escaped != 0) ||
            !float.IsFinite(data.NextDispatch) || data.NextDispatch < 0 ||
            data.NextDispatch > data.Elapsed + Simulation.DispatchCooldown + TimeTolerance ||
            data.Capture == null || data.SecuredFor == null || data.SecuredFor.Length != data.Capture.Length ||
            data.SecuredFor.Where((t, i) => !float.IsFinite(t) || t < 0 || t > Campaign.MaxRecordedSeconds || (t > 0 && data.Capture[i] < CaptureSite.HoldSeconds)).Any())
        {
            throw new FormatException("Invalid enemy command data.");
        }

        sim.NextDispatch = data.NextDispatch;
        sim.WavesLanded = data.WavesLanded;
        sim.Escaped = data.Escaped;
        var columnKinds = sim.ZoneBattle?.Columns?.SelectMany(c => c.Kinds).ToList() ?? [];
        for (int i = 0; i < Math.Min(sim.Sites.Count, data.SecuredFor.Length); i++)
        {
            sim.Sites[i].SecuredFor = data.SecuredFor[i];
        }

        // Placeholders that the saved entries are validated against and then applied to. A Classic
        // column vehicle keeps the id it arrived with (escapes leave gaps) and its column's kind.
        int previous = Mission.FirstReinforcementId - 1;
        for (int n = 0; n < landed; n++)
        {
            int id = Mission.FirstReinforcementId + n;
            var kind = VehicleKind.Defender;
            if (sim.Classic)
            {
                id = data.Vehicles![deployed + n]?.Id ?? -1;
                if (id <= previous || id >= Mission.FirstReinforcementId + arrived)
                {
                    throw new FormatException("Invalid enemy column data.");
                }

                kind = columnKinds[id - Mission.FirstReinforcementId];
                previous = id;
            }

            sim.Vehicles.Add(new Vehicle
            {
                Id = id,
                Enemy = true,
                Group = Mission.EnemyGroup,
                Kind = kind,
            });
        }
    }

    /// <summary>Restores Classic missions, mines, smoke and fire missions in flight, after <c>Elapsed</c> is set.</summary>
    private static void RestoreSupport(Snapshot data, Simulation sim)
    {
        bool Timely(float t) => float.IsFinite(t) && t >= 0 && t <= sim.Elapsed + Simulation.SmokeSeconds + Simulation.ArtilleryDelay + Simulation.MineArmSeconds + TimeTolerance;
        if (data.ArtilleryLeft < 0 || data.ArtilleryLeft > Simulation.PlayerArtilleryMissions ||
            data.EnemyArtilleryLeft < 0 || data.EnemyArtilleryLeft > Mission.Count ||
            data.MinesLeft < 0 || data.MinesLeft > Simulation.PlayerMines ||
            !float.IsFinite(data.NextEnemyArtillery) || data.NextEnemyArtillery < 0 ||
            data.Mines == null || data.Mines.Count > Simulation.PlayerMines + Mission.MinesPerField ||
            data.Mines.Any(m => m == null || !Navigation.Inside(m.Position) || !Timely(m.ArmedAt)) ||
            data.Smoke == null || data.Smoke.Any(c => c == null || !Navigation.Inside(c.Position) || !float.IsFinite(c.Radius) || c.Radius <= 0 || !Timely(c.Until)) ||
            data.Strikes == null || data.Strikes.Any(s => s == null || !Navigation.Inside(s.Target) || !Enum.IsDefined(s.Shell) ||
                s.RoundsLeft <= 0 || s.RoundsLeft > Simulation.ArtilleryRounds || !Timely(s.NextRound)))
        {
            throw new FormatException("Invalid support weapon data.");
        }

        sim.ArtilleryLeft = data.ArtilleryLeft;
        sim.EnemyArtilleryLeft = data.EnemyArtilleryLeft;
        sim.MinesLeft = data.MinesLeft;
        sim.NextEnemyArtillery = data.NextEnemyArtillery;
        sim.Mines.Clear();
        sim.Mines.AddRange(data.Mines);
        sim.Smoke.Clear();
        sim.Smoke.AddRange(data.Smoke);
        sim.Strikes.Clear();
        sim.Strikes.AddRange(data.Strikes);
    }

    /// <summary>Restores building damage (knocking down any rubble) and the battle's bounties and fines.</summary>
    private static void RestoreEconomy(Snapshot data, Simulation sim)
    {
        const int maxFunds = 1000000;
        if (data.BuildingHealth == null || data.BuildingHealth.Length != sim.Layout.Count ||
            data.BuildingHealth.Any(h => !float.IsFinite(h) || h < 0 || h > Vehicle.MaxHealth) ||
            data.BuildingHealth.Where((h, i) => h < Vehicle.MaxHealth && sim.Layout[i].Kind != ObstacleKind.Building).Any() ||
            data.Bounty < 0 || data.Bounty > maxFunds || data.Fines < 0 || data.Fines > maxFunds)
        {
            throw new FormatException("Invalid economy data.");
        }

        sim.BuildingHealth = [.. data.BuildingHealth];
        sim.ApplyBuildingDamage();
        sim.Bounty = data.Bounty;
        sim.Fines = data.Fines;
    }

    private sealed class Snapshot
    {
        public int Version { get; set; } = CurrentVersion;

        public int ArtilleryLeft { get; set; }
        public int Bounty { get; set; }
        public float[]? BuildingHealth { get; set; }
        public Campaign? Campaign { get; set; }
        public float[] Capture { get; set; } = [];
        public float Elapsed { get; set; }
        public int EnemyArtilleryLeft { get; set; }
        public int Escaped { get; set; }
        public int Fines { get; set; }
        public List<Mine>? Mines { get; set; }
        public int MinesLeft { get; set; }
        public float NextEnemyArtillery { get; set; }
        public float NextDispatch { get; set; }
        public MissionPhase Phase { get; set; } = MissionPhase.Active;
        public ReconSnapshot? Recon { get; set; }
        public float[]? SecuredFor { get; set; }
        public List<SmokeCloud>? Smoke { get; set; }
        public List<ArtilleryStrike>? Strikes { get; set; }
        public VehicleSave[] Vehicles { get; set; } = [];
        public int WavesLanded { get; set; }
    }

    /// <summary>The persisted part of a vehicle. Route, speed and AI timers are rebuilt on load.</summary>
    private sealed class VehicleSave
    {
        public int Id { get; set; }

        public int Ammo { get; set; }
        public bool Enemy { get; set; }
        public int Group { get; set; }
        public float Heading { get; set; }
        public float Health { get; set; }
        public bool HoldFire { get; set; }

        /// <summary>Written only when the post differs from where the campaign deploys this vehicle.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Vector2? Home { get; set; }

        public VehicleKind Kind { get; set; }
        public Weapon Loaded { get; set; }
        public int Objective { get; set; } = Vehicle.NoObjective;
        public List<Vector2>? Orders { get; set; }
        public int PatrolLeg { get; set; }
        public Vector2 Position { get; set; }
        public float Reload { get; set; }
        public float Revealed { get; set; }
        public int Smoke { get; set; }

        /// <summary>Classic rounds per ammunition type; absent for Modern vehicles.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[]? Rounds { get; set; }

        public Tactic Tactic { get; set; }
        public float TurretHeading { get; set; }
        public bool Waiting { get; set; }

        public static VehicleSave From(Vehicle v, Vehicle? deployed) => new()
        {
            Id = v.Id,
            Ammo = v.Ammo,
            Enemy = v.Enemy,
            Group = v.Group,
            Heading = v.Heading,
            Health = v.Health,
            HoldFire = v.HoldFire,
            Home = v.Home == deployed?.Home ? null : v.Home,
            Kind = v.Kind,
            Loaded = v.Loaded,
            Objective = v.Objective,
            Orders = [.. v.Orders],
            PatrolLeg = v.PatrolLeg,
            Position = v.Position,
            Reload = v.Reload,
            Revealed = v.Revealed,
            Smoke = v.Smoke,
            Rounds = v.Spec.Classic ? [.. v.Rounds] : null,
            Tactic = v.Tactic,
            TurretHeading = v.TurretHeading,
            Waiting = v.Waiting,
        };

        /// <summary>Checks this entry against the vehicle the saved campaign deploys in the same slot.</summary>
        public bool IsValidFor(Vehicle deployed, Simulation sim, int version)
        {
            var spec = VehicleSpec.For(Kind);
            // Landed reinforcements have no deployed post, so theirs must be saved.
            bool reinforcement = Id >= Mission.FirstReinforcementId && Id < Mission.FirstConvoyId;
            bool homeValid = version < 4 ? !reinforcement : Home is { } home ? Geometry.Finite(home) && Navigation.Inside(home) : !reinforcement;
            bool objectiveValid = version < 4 || Objective == Vehicle.NoObjective || (Enemy && Objective >= 0 && Objective < sim.Sites.Count);
            bool classicValid = spec.Classic
                ? Rounds != null && Rounds.Length == spec.Magazine.Count && Rounds.Select((n, w) => n >= 0 && n <= spec.Magazine[w]).All(ok => ok) &&
                    Enum.IsDefined(Loaded) && float.IsFinite(Revealed) && Revealed >= 0 && Revealed <= ClassicRules.RevealSeconds &&
                    Smoke >= 0 && Smoke <= spec.SmokeRounds
                : Rounds == null && Revealed == 0 && Smoke == 0 && !Waiting;
            return Id == deployed.Id && Group == deployed.Group && Enemy == deployed.Enemy && Kind == deployed.Kind &&
                homeValid && objectiveValid && classicValid &&
                sim.Navigation.Clear(Position, Position, Mission.VehicleRadius) &&
                float.IsFinite(Health) && Health >= 0 && Health <= Vehicle.MaxHealth &&
                Ammo >= 0 && Ammo <= spec.Ammo &&
                float.IsFinite(Heading) && float.IsFinite(TurretHeading) &&
                float.IsFinite(Reload) && Reload >= 0 && Reload <= spec.Reload &&
                Enum.IsDefined(Tactic) && PatrolLeg is 0 or 1 &&
                Orders != null && Orders.Count <= Vehicle.MaxOrders && Orders.All(p => Navigation.Inside(p));
        }

        public void ApplyTo(Vehicle v, int version)
        {
            v.Ammo = Ammo;
            v.Heading = Heading;
            v.Health = Health;
            v.Loaded = Loaded;
            v.HoldFire = HoldFire;
            if (version >= 4)
            {
                v.Home = Home ?? v.Home;
                v.Objective = Objective;
            }

            v.Orders = [.. Orders ?? []];
            v.PatrolLeg = PatrolLeg;
            v.Position = Position;
            v.Reload = Reload;
            v.Revealed = Revealed;
            if (version >= 6)
            {
                v.Smoke = Smoke;
            }

            v.Rounds = [.. Rounds ?? []];
            v.Route = [];
            v.Speed = 0;
            v.Status = Orders is { Count: > 0 } ? VehicleStatus.Moving : VehicleStatus.Holding;
            v.Stuck = 0;
            v.Tactic = Tactic;
            v.Think = 0;
            v.TurretHeading = TurretHeading;
            v.Waiting = Waiting;
        }
    }
}
