namespace Breakwater.Core;

/// <summary>
/// Deployment state carried between islands: the sixteen roster slots, supplies and running totals.
/// A <see cref="Simulation"/> clones this and never mutates it, so retrying restores the same force.
/// </summary>
public sealed class Campaign
{
    public const int StartingSupplies = 100;
    public const int VictorySupplies = 100;
    public const int MainTankPrice = 40;
    public const int ScoutPrice = 25;

    // Classic funds are counted in units of $10,000, as the reference prices are: an M1 is $2.5 million.
    public const int ClassicStartingFunds = 5500;
    public const int ClassicVictoryPay = 1000;
    public const int CivilianFine = 200;
    public const int AbramsPrice = 250;
    public const int BradleyPrice = 150;
    public const int ItvPrice = 100;
    public const int M113Price = 40;

    // Enemy prices: what the bounty on each pays.
    public const int T72Price = 150;
    public const int T62Price = 80;
    public const int BmpPrice = 20;
    public const int BtrPrice = 60;

    /// <summary>Classic repairs cost a vehicle's price for this many points of health (an M1: one unit per point).</summary>
    public const float ClassicRepairPoints = 250;

    /// <summary>Dollars per Classic funds unit.</summary>
    public const int DollarsPerFund = 10000;

    /// <summary>Supplies per point of missing health, rounded up per vehicle.</summary>
    public const float RepairCostPerHealth = .15f;

    // Sanity limits for validating saves; far beyond anything reachable in play.
    private const int MaxSupplies = 1000000;
    private const int MaxLosses = Mission.FriendlySlots * Mission.Count * MaxBattlesPerIsland;

    /// <summary>For validating saves: no island has more battles than this.</summary>
    private const int MaxBattlesPerIsland = 8;
    public static readonly float MaxRecordedSeconds = (float)TimeSpan.FromDays(100).TotalSeconds;

    /// <summary>The battle within the island (Classic mode fights several per island). Older saves have none: the first.</summary>
    public int Battle { get; set; }

    public int Island { get; set; }

    /// <summary>The rule set. Saves from before Classic mode existed have no value and play Modern.</summary>
    public GameMode Mode { get; set; }

    public int PreviousLosses { get; set; }
    public float PreviousTime { get; set; }

    public RosterSlot[] Roster { get; set; } = DefaultRoster(GameMode.Modern);

    public int Supplies { get; set; } = StartingSupplies;

    /// <summary>A fresh campaign in <paramref name="mode"/>, with that mode's starting force.</summary>
    public static Campaign Start(GameMode mode) => new()
    {
        Mode = mode,
        Roster = DefaultRoster(mode),
        Supplies = mode == GameMode.Classic ? ClassicStartingFunds : StartingSupplies,
    };

    /// <summary>An amount as the player sees it: supplies in Modern mode, dollars in Classic.</summary>
    public static string Money(int amount, GameMode mode) => mode == GameMode.Classic
        ? amount == 0 ? "$0" : amount * (long)DollarsPerFund >= 1000000 ? $"${amount * (double)DollarsPerFund / 1000000:0.##}M" : $"${amount * DollarsPerFund / 1000}k"
        : $"{amount} supplies";

    public static int Price(VehicleKind kind) => kind switch
    {
        VehicleKind.MainTank => MainTankPrice,
        VehicleKind.Abrams => AbramsPrice,
        VehicleKind.Bradley => BradleyPrice,
        VehicleKind.Itv => ItvPrice,
        VehicleKind.M113 => M113Price,
        VehicleKind.T72 => T72Price,
        VehicleKind.T62 => T62Price,
        VehicleKind.Bmp => BmpPrice,
        VehicleKind.Btr => BtrPrice,
        _ => ScoutPrice,
    };

    /// <summary>The vehicle a group fitted out as <paramref name="loadout"/> has in each slot.</summary>
    public static VehicleKind KindFor(GameMode mode, Loadout loadout, int slotInGroup)
    {
        bool rear = slotInGroup >= 2;
        return (mode, loadout) switch
        {
            (GameMode.Classic, Loadout.Recon) => rear ? VehicleKind.M113 : VehicleKind.Bradley,
            // Missile carriers fight from behind the tanks.
            (GameMode.Classic, Loadout.Mixed) => rear ? VehicleKind.Itv : VehicleKind.Abrams,
            (GameMode.Classic, _) => VehicleKind.Abrams,
            (_, Loadout.Recon) => VehicleKind.Scout,
            // Mixed keeps main tanks in the two lead slots.
            (_, Loadout.Mixed) => rear ? VehicleKind.Scout : VehicleKind.MainTank,
            _ => VehicleKind.MainTank,
        };
    }

    public Campaign Clone() => new()
    {
        Battle = Battle,
        Island = Island,
        Mode = Mode,
        PreviousLosses = PreviousLosses,
        PreviousTime = PreviousTime,
        Roster = Roster.Select(s => new RosterSlot { Id = s.Id, Health = s.Health, Kind = s.Kind }).ToArray(),
        Supplies = Supplies,
    };

    public bool Valid() =>
        Island >= 0 && Island < Mission.Count && Enum.IsDefined(Mode) &&
        Battle >= 0 && Battle < Mission.BattleCount(this) &&
        Supplies >= 0 && Supplies <= MaxSupplies &&
        float.IsFinite(PreviousTime) && PreviousTime >= 0 && PreviousTime <= MaxRecordedSeconds &&
        PreviousLosses >= 0 && PreviousLosses <= MaxLosses &&
        Roster != null && Roster.Length == Mission.FriendlySlots &&
        Roster.Select((r, i) => r != null && r.Id == i && Enum.GetValues<Loadout>().Any(l => KindFor(Mode, l, Mission.SlotInGroup(i)) == r.Kind) &&
            float.IsFinite(r.Health) && r.Health >= 0 && r.Health <= Vehicle.MaxHealth).All(v => v);

    /// <summary>The loadout whose vehicles match the group's slots; a mixture of loadouts counts as <see cref="Loadout.Mixed"/>.</summary>
    public Loadout LoadoutOf(int group) => Enum.GetValues<Loadout>()
        .Where(l => GroupSlots(group).All(r => r.Kind == KindFor(Mode, l, Mission.SlotInGroup(r.Id))))
        .DefaultIfEmpty(Loadout.Mixed)
        .First();

    public Loadout NextLoadout(int group) => LoadoutOf(group) switch
    {
        Loadout.Armor => Loadout.Mixed,
        Loadout.Mixed => Loadout.Recon,
        _ => Loadout.Armor,
    };

    /// <summary>Supplies needed to refit; negative when the refit returns supplies. Lost slots are free.</summary>
    public int RefitCost(int group)
    {
        var next = NextLoadout(group);
        return GroupSlots(group).Where(r => r.Health > 0)
            .Sum(r => Price(KindFor(Mode, next, Mission.SlotInGroup(r.Id))) - Price(r.Kind));
    }

    public bool Refit(int group)
    {
        if (group < 0 || group >= Mission.GroupCount)
        {
            return false;
        }

        int cost = RefitCost(group);
        if (cost > Supplies)
        {
            return false;
        }

        var next = NextLoadout(group);
        foreach (var r in GroupSlots(group))
        {
            r.Kind = KindFor(Mode, next, Mission.SlotInGroup(r.Id));
        }

        Supplies -= cost;
        return true;
    }

    public int RepairCost(int group) =>
        GroupSlots(group).Where(r => r.Health > 0).Sum(r => (int)MathF.Ceiling((Vehicle.MaxHealth - r.Health) * RepairRate(r.Kind)));

    /// <summary>Funds per point of health repaired: flat in Modern mode, in proportion to the vehicle's price in Classic.</summary>
    private float RepairRate(VehicleKind kind) => Mode == GameMode.Classic ? Price(kind) / ClassicRepairPoints : RepairCostPerHealth;

    public bool Repair(int group)
    {
        int cost = RepairCost(group);
        if (cost <= 0 || cost > Supplies)
        {
            return false;
        }

        Supplies -= cost;
        foreach (var r in GroupSlots(group).Where(r => r.Health > 0))
        {
            r.Health = Vehicle.MaxHealth;
        }

        return true;
    }

    public int ReplacementCost(int group)
    {
        var lost = FirstLost(group);
        return lost == null ? 0 : Price(lost.Kind);
    }

    public bool Replace(int group)
    {
        var lost = FirstLost(group);
        if (lost == null || Price(lost.Kind) > Supplies)
        {
            return false;
        }

        Supplies -= Price(lost.Kind);
        lost.Health = Vehicle.MaxHealth;
        return true;
    }

    public Campaign AfterVictory(Simulation battle)
    {
        if (battle.Phase != MissionPhase.Victory || battle.Campaign != this || Mission.FinalBattle(this))
        {
            throw new InvalidOperationException("There is no next battle available.");
        }

        var next = Clone();
        if (Battle + 1 < Mission.BattleCount(this))
        {
            next.Battle++;
        }
        else
        {
            next.Island++;
            next.Battle = 0;
        }

        next.Supplies += Mode == GameMode.Classic ? Math.Max(0, ClassicVictoryPay + battle.Bounty - battle.Fines) : VictorySupplies;
        next.PreviousTime += battle.Elapsed;
        foreach (var slot in next.Roster)
        {
            var vehicle = battle.Vehicles.FirstOrDefault(v => !v.Enemy && v.Id == slot.Id);
            if (slot.Health > 0 && vehicle is { Alive: false })
            {
                next.PreviousLosses++;
            }

            slot.Health = vehicle?.Health ?? 0;
        }
        return next;
    }

    // Modern: Bravo and Delta start with two scouts; Alpha and Charlie are all main tanks.
    // Classic: Alpha has two missile carriers, Bravo and Charlie are all tanks, Delta is the light group.
    private static Loadout DefaultLoadout(GameMode mode, int group) => (mode, group) switch
    {
        (GameMode.Classic, 0) => Loadout.Mixed,
        (GameMode.Classic, Mission.GroupCount - 1) => Loadout.Recon,
        (GameMode.Classic, _) => Loadout.Armor,
        _ => group % 2 == 1 ? Loadout.Mixed : Loadout.Armor,
    };

    private static RosterSlot[] DefaultRoster(GameMode mode) => Enumerable.Range(0, Mission.FriendlySlots)
        .Select(id => new RosterSlot { Id = id, Kind = KindFor(mode, DefaultLoadout(mode, Mission.GroupOf(id)), Mission.SlotInGroup(id)) })
        .ToArray();

    private IEnumerable<RosterSlot> GroupSlots(int group) => Roster.Where(r => Mission.GroupOf(r.Id) == group);

    private RosterSlot? FirstLost(int group) => GroupSlots(group).FirstOrDefault(r => r.Health == 0);
}
