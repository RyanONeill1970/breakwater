namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// Island layouts and force structure. World units are metres; X runs east, Y runs south, and the
/// playable island is the square ±<see cref="Edge"/> on both axes.
/// </summary>
public static class Mission
{
    /// <summary>Islands in the campaign.</summary>
    public const int Count = 3;

    public const int GroupCount = 4;
    public const int VehiclesPerGroup = 4;
    public const int FriendlySlots = GroupCount * VehiclesPerGroup;
    public const int SitesPerIsland = 3;
    public const int DefendersPerSite = 4;
    public const int DefenderCount = SitesPerIsland * DefendersPerSite;

    /// <summary>Group index shared by every defender.</summary>
    public const int EnemyGroup = GroupCount;

    /// <summary>Group index of a Classic escort battle's supply trucks, which the player cannot command.</summary>
    public const int ConvoyGroup = GroupCount + 1;

    /// <summary>Supply trucks take ids from here, clear of the roster, the defenders and any arrivals.</summary>
    public const int FirstConvoyId = 100;

    /// <summary>Supply trucks wait at the start of their route in a row this far apart.</summary>
    private const float ConvoySpacing = 6;

    /// <summary>Friendly roster slots use ids 0–15; defenders follow on from here.</summary>
    public const int FirstDefenderId = FriendlySlots;

    /// <summary>Reinforcements landed during the battle take ids after the deployed defenders.</summary>
    public const int FirstReinforcementId = FirstDefenderId + DefenderCount;

    /// <summary>Defenders in each reinforcement wave.</summary>
    public const int WaveSize = 3;

    /// <summary>Reinforcement waves each island's defenders call in; later islands fight harder.</summary>
    public static readonly int[] ReinforcementWaves = [1, 2, 2];

    /// <summary>Half-width of the playable square, in metres.</summary>
    public const float Edge = 92;

    /// <summary>Collision radius of every hull, in metres.</summary>
    public const float VehicleRadius = 2.1f;

    /// <summary>Friendly vehicles south of this line (greater Y) are in the beach supply zone.</summary>
    public const float SupplyLineY = 64;

    public static readonly string[] Names = ["OPERATION REEF", "OPERATION BREAKWATER", "OPERATION SIGNAL"];

    public static readonly string[] Briefings =
    [
        "Secure the southern relay, western station and northern command post. Wide approaches make this a good place to learn coordinated attacks.",
        "Capture the harbor relay, eastern depot and western radio station. Warehouses and long ridges divide the routes: use both sides of the island.",
        "Seize the eastern relay, western garrison and the northern transmitter. Preserve your remaining armor for the final assault through the central gaps.",
    ];

    public static readonly string[] GroupNames = ["ALPHA", "BRAVO", "CHARLIE", "DELTA"];

    // Friendly deployment grid on the southern beach: groups side by side, each a 2 × 2 block.
    private const float DeployWest = -65;
    private const float DeployNorth = 72;
    private const float DeployGroupSpacing = 24;
    private const float DeployColumnSpacing = 7;
    private const float DeployRowSpacing = 8;

    // Defenders line up just north of the site they guard.
    private const float DefenderSpacing = 7;
    private const float DefenderThinkStagger = .3f;
    private static readonly Vector2 DefenderLineOffset = new(-10, -12);

    /// <summary>Where each island's reinforcements come ashore: the north shore, away from the landing beach.</summary>
    private static readonly Vector2[] LandingPoints = [new(0, -86), new(20, -86), new(-35, -86)];

    public static Vector2 LandingPoint(int island) => LandingPoints[island];

    /// <summary>
    /// Classic enemy minefields: a line of mines across a road the player is likely to use, one per
    /// island. Each is given as its middle and the step from one mine to the next.
    /// </summary>
    private static readonly (Vector2 Middle, Vector2 Step)[] Minefields =
    [
        (new(-35, -14.4f), new(2, -3.5f)),
        (new(18, 34.4f), new(3, 2.6f)),
        (new(-2, 39.3f), new(2.6f, -3)),
    ];

    /// <summary>Mines in each enemy minefield.</summary>
    public const int MinesPerField = 5;

    /// <summary>Where the enemy's mines lie on <paramref name="island"/>.</summary>
    public static IEnumerable<Vector2> Minefield(int island)
    {
        var (middle, step) = Minefields[island];
        return Enumerable.Range(0, MinesPerField).Select(n => middle + step * (n - (MinesPerField - 1) / 2f));
    }

    // Classic mission conditions (see ClassicCampaign): a loss limit, a time limit, a village to spare.
    public const int ClassicLossLimit = 4;
    public const float ClassicTimeLimitSeconds = 600;

    /// <summary>The Classic battle being fought, or null in Modern mode.</summary>
    public static ZoneBattle? ZoneBattle(Campaign campaign) =>
        campaign.Mode == GameMode.Classic ? ClassicCampaign.Battle(campaign.Island, campaign.Battle) : null;

    /// <summary>Battles on the campaign's current island: one in Modern mode.</summary>
    public static int BattleCount(Campaign campaign) =>
        campaign.Mode == GameMode.Classic ? ClassicCampaign.BattleCount(campaign.Island) : 1;

    /// <summary>Whether this is the campaign's last battle.</summary>
    public static bool FinalBattle(Campaign campaign) => campaign.Island == Count - 1 && campaign.Battle == BattleCount(campaign) - 1;

    /// <summary>The terms this battle must be won on; none in Modern mode.</summary>
    public static BattleConditions Conditions(Campaign campaign) => ZoneBattle(campaign)?.Conditions ?? new();

    /// <summary>Capture sites for the campaign's current battle.</summary>
    public static List<CaptureSite> Sites(Campaign campaign) => ZoneBattle(campaign) is ZoneBattle battle
        ? battle.Sites.Select(s => new CaptureSite(s.Name, s.Position)).ToList()
        : Sites(campaign.Island);

    /// <summary>How the radio names a friendly vehicle's unit: its group, or the convoy.</summary>
    public static string UnitName(Vehicle vehicle) => vehicle.Convoy ? "CONVOY" : GroupNames[vehicle.Group];

    /// <summary>Where the <paramref name="n"/>th supply truck waits: in a row east of the route's start.</summary>
    public static Vector2 ConvoyPost(EscortConvoy convoy, int n) => convoy.Route[0] + new Vector2(n * ConvoySpacing, 0);

    /// <summary>Where the <paramref name="n"/>th supply truck parks at the end of the route, in a row centred on it.</summary>
    public static Vector2 ConvoyDestination(EscortConvoy convoy, int n) => convoy.Route[^1] + new Vector2((n - (convoy.Trucks - 1) / 2f) * ConvoySpacing, 0);

    /// <summary>The <paramref name="n"/>th defender post at a site: a line just north of it.</summary>
    public static Vector2 DefenderPost(Vector2 site, int n) => site + DefenderLineOffset + new Vector2(n * DefenderSpacing, 0);

    /// <summary>The Classic island fought at night.</summary>
    public const int ClassicNightIsland = 1;

    /// <summary>Whether this battle is fought at night: one island of a Classic campaign.</summary>
    public static bool Night(Campaign campaign) => campaign.Mode == GameMode.Classic && campaign.Island == ClassicNightIsland;

    /// <summary>The most reinforcements that can land on an island.</summary>
    public static int MaxReinforcements(int island) => ReinforcementWaves[island] * WaveSize;

    public static int GroupOf(int slotId) => slotId / VehiclesPerGroup;

    public static int SlotInGroup(int slotId) => slotId % VehiclesPerGroup;

    public static List<CaptureSite> Sites(int island = 0) => island switch
    {
        1 =>
        [
            new("HARBOR RELAY", new(-42, 30)),
            new("EAST DEPOT", new(45, -20)),
            new("WEST RADIO", new(-40, -62)),
        ],
        2 =>
        [
            new("EAST RELAY", new(40, 28)),
            new("WEST GARRISON", new(-46, -20)),
            new("TRANSMITTER", new(5, -64)),
        ],
        _ =>
        [
            new("SOUTH RELAY", new(16, 28)),
            new("WEST STATION", new(-56, -36)),
            new("NORTH COMMAND", new(49, -58)),
        ],
    };

    public static List<Obstacle> Obstacles(int island = 0) => island switch
    {
        1 =>
        [
            new(new(-29, 30), new(5, 4), 5, ObstacleKind.Building),
            new(new(58, -20), new(5, 4), 5, ObstacleKind.Building),
            new(new(-27, -62), new(5, 4), 5, ObstacleKind.Building),
            new(new(-10, 25), new(6, 20), 7, ObstacleKind.Ridge),
            new(new(12, -30), new(8, 17), 7, ObstacleKind.Ridge),
            new(new(-56, -20), new(17, 5), 6, ObstacleKind.Ridge),
            new(new(60, 44), new(8, 6), 5, ObstacleKind.Building),
            new(new(-62, 52), new(8, 4), 4, ObstacleKind.Building),
            new(new(-66, -70), new(7, 9), 7, ObstacleKind.Ridge),
        ],
        2 =>
        [
            new(new(53, 28), new(5, 4), 5, ObstacleKind.Building),
            new(new(-33, -20), new(5, 4), 5, ObstacleKind.Building),
            new(new(18, -64), new(5, 4), 6, ObstacleKind.Building),
            new(new(-20, 30), new(10, 13), 7, ObstacleKind.Ridge),
            new(new(3, -4), new(22, 5), 8, ObstacleKind.Ridge),
            new(new(57, -23), new(7, 20), 7, ObstacleKind.Ridge),
            new(new(-58, -60), new(13, 5), 6, ObstacleKind.Ridge),
            new(new(-10, -42), new(6, 8), 5, ObstacleKind.Building),
        ],
        _ =>
        [
            new(new(29, 28), new(5, 4), 5, ObstacleKind.Building),
            new(new(-43, -35), new(5, 4), 5, ObstacleKind.Building),
            new(new(62, -57), new(5, 4), 5, ObstacleKind.Building),
            new(new(-27, 15), new(7, 20), 7, ObstacleKind.Ridge),
            new(new(33, -8), new(19, 5), 6, ObstacleKind.Ridge),
            new(new(-63, 16), new(7, 5), 4, ObstacleKind.Building),
            new(new(-65, -65), new(10, 5), 6, ObstacleKind.Ridge),
            new(new(-12, -57), new(6, 13), 8, ObstacleKind.Ridge),
            new(new(72, 52), new(6, 7), 5, ObstacleKind.Ridge),
        ],
    };

    public static List<Vehicle> Vehicles(Campaign? campaign = null)
    {
        campaign ??= new Campaign();
        List<Vehicle> units = [];
        for (int g = 0; g < GroupCount; g++)
        {
            for (int n = 0; n < VehiclesPerGroup; n++)
            {
                var slot = campaign.Roster[g * VehiclesPerGroup + n];
                if (slot.Health <= 0)
                {
                    continue;
                }

                var position = new Vector2(
                    DeployWest + g * DeployGroupSpacing + (n % 2) * DeployColumnSpacing,
                    DeployNorth + (n / 2) * DeployRowSpacing);
                var vehicle = new Vehicle
                {
                    Id = slot.Id,
                    Group = g,
                    Health = slot.Health,
                    Home = position,
                    Kind = slot.Kind,
                    Position = position,
                    // Classic groups land waiting too, so neither side starts the battle by accident.
                    Waiting = campaign.Mode == GameMode.Classic,
                };
                vehicle.Rearm();
                units.Add(vehicle);
            }
        }

        var garrison = ZoneBattle(campaign)?.Garrison ?? Sites(campaign.Island)
            .SelectMany(site => Enumerable.Range(0, DefendersPerSite).Select(n => new Garrison(DefenderPost(site.Position, n), VehicleKind.Defender)))
            .ToList();
        for (int i = 0; i < garrison.Count; i++)
        {
            var defender = new Vehicle
            {
                Id = FirstDefenderId + i,
                Enemy = true,
                Group = EnemyGroup,
                // Defenders face south, towards the landing beach.
                Heading = MathF.PI,
                Home = garrison[i].Post,
                Kind = garrison[i].Kind,
                Position = garrison[i].Post,
                Think = i % DefendersPerSite * DefenderThinkStagger,
                TurretHeading = MathF.PI,
                // A Classic garrison waits until provoked (see ClassicRules.EngageRange).
                Waiting = campaign.Mode == GameMode.Classic,
            };
            defender.Rearm();
            units.Add(defender);
        }

        if (ZoneBattle(campaign)?.Convoy is EscortConvoy convoy)
        {
            for (int n = 0; n < convoy.Trucks; n++)
            {
                var truck = new Vehicle
                {
                    Id = FirstConvoyId + n,
                    Group = ConvoyGroup,
                    // The trucks do not fight; they only drive.
                    HoldFire = true,
                    Home = ConvoyPost(convoy, n),
                    Kind = VehicleKind.Truck,
                    Position = ConvoyPost(convoy, n),
                };
                truck.Rearm();
                units.Add(truck);
            }
        }

        return units;
    }
}
