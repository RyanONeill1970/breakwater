namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// The Classic campaign: each island is fought zone by zone, several battles in turn. The first
/// island opens with two training battles. Every island ends with the battle for all its stations.
/// </summary>
public static class ClassicCampaign
{
    /// <summary>Seconds a hold battle's site must be held into the battle.</summary>
    public const float HoldSeconds = 180;

    /// <summary>The share of an ambushed convoy that must be destroyed.</summary>
    public const float AmbushQuota = .6f;

    // Garrisons at the stations, one kind per defender post, heavier on later islands.
    private static readonly VehicleKind[][] StationGarrisons =
    [
        [VehicleKind.T62, VehicleKind.T62, VehicleKind.Bmp, VehicleKind.Btr],
        [VehicleKind.T72, VehicleKind.T62, VehicleKind.Bmp, VehicleKind.Bmp],
        [VehicleKind.T72, VehicleKind.T72, VehicleKind.T62, VehicleKind.Bmp],
    ];

    /// <summary>The battles of each island, in order.</summary>
    private static readonly ZoneBattle[][] Battles =
    [
        [
            new(
                "TRAINING: THE ROAD NORTH",
                "Drive to the south relay and hold it. Nobody is defending it. Roads are fast; open ground is slower, jungle slower still, " +
                "and the river is crossed quickest at the bridge. Right-click to move, and watch how your groups pick the road.",
                ObjectiveKind.Capture, 1, [("SOUTH RELAY", new(16, 28))], [], Training: true),
            new(
                "TRAINING: GUNNERY RANGE",
                "Four old carriers stand as targets in the north-west. Destroy them all. Press Tab to look through the leader's gun sight: " +
                "the mouse lays the gun, a click fires and right-click changes ammunition. Try calling artillery on them with B.",
                ObjectiveKind.Clear, 2, [],
                [
                    new(new(-52, -2), VehicleKind.Btr),
                    new(new(-44, 2), VehicleKind.Btr),
                    new(new(-60, 4), VehicleKind.Btr),
                    new(new(-48, -12), VehicleKind.Btr),
                ],
                Training: true),
            new(
                "REEF: AMBUSH ON THE COAST ROAD",
                "An enemy supply convoy will come down from the north-east, cross the island by the coast road and leave by the west shore. " +
                "Destroy at least three of its five vehicles before they get away. Its own road is clear of mines; jungle edges beside it make good ambush positions.",
                ObjectiveKind.Ambush, 3, [], [],
                Columns:
                [
                    new(25,
                        [VehicleKind.Btr, VehicleKind.Bmp, VehicleKind.Btr, VehicleKind.T62, VehicleKind.Btr],
                        [new(50, -86), new(50, -50), new(58, -30), new(58, -2), new(22, 24), new(0, 5), new(-45, -20), new(-56, -30), new(-88, -30)]),
                ],
                AmbushQuota: AmbushQuota,
                EnemyMines: false),
            new(
                "OPERATION REEF",
                Mission.Briefings[0],
                ObjectiveKind.Capture, 0, Stations(0), StationGarrison(0),
                Conditions: new(MaxLosses: Mission.ClassicLossLimit)),
        ],
        [
            new(
                "BREAKWATER: THE RADAR",
                "A radar beside the east depot watches the whole harbour. Knock it down before dawn; its guards are dug in around it.",
                ObjectiveKind.Destroy, 3, [],
                [
                    new(new(44, -12), VehicleKind.T62),
                    new(new(44, -28), VehicleKind.T62),
                    new(new(38, -20), VehicleKind.Bmp),
                    new(new(50, -34), VehicleKind.Bmp),
                ],
                Target: 1,
                Conditions: new(TimeLimitSeconds: Mission.ClassicTimeLimitSeconds)),
            new(
                "BREAKWATER: HOLD THE SEAWALL",
                "Take the harbour relay and hold it. Two enemy columns will come ashore in the north and drive on it through the night. " +
                $"The relay must be yours when {HoldSeconds / 60:0} minutes have passed.",
                ObjectiveKind.Hold, 0, [("SEAWALL", new(-42, 30))], [],
                Columns:
                [
                    new(30, [VehicleKind.T62, VehicleKind.Bmp, VehicleKind.Bmp], [new(-35, -86), new(-38, -56), new(-30, 0), new(-38, 24)]),
                    new(100, [VehicleKind.T72, VehicleKind.T62, VehicleKind.Bmp], [new(0, -86), new(-10, -50), new(-25, -20), new(-38, 24)]),
                ],
                HoldSeconds: HoldSeconds),
            new(
                "OPERATION BREAKWATER",
                Mission.Briefings[1],
                ObjectiveKind.Capture, 2, Stations(1), StationGarrison(1)),
        ],
        [
            new(
                "SIGNAL: CLEAR THE JUNGLE",
                "Armour is hiding along the jungle edges on both sides of the island. Hunt down every vehicle, " +
                "and spare the village: its buildings must still be standing when you are done.",
                ObjectiveKind.Clear, 2, [],
                [
                    new(new(-47, 12), VehicleKind.T72),
                    new(new(-60, 5), VehicleKind.Bmp),
                    new(new(-72, 10), VehicleKind.T62),
                    new(new(32, -28), VehicleKind.Bmp),
                    new(new(20, -44), VehicleKind.T72),
                    new(new(44, -40), VehicleKind.Btr),
                ],
                Conditions: new(SpareCivilians: true)),
            new(
                "SIGNAL: ESCORT THE SUPPLY CONVOY",
                "Four supply trucks will leave the beach after 40 seconds and drive the road north-west to the western garrison's old depot, " +
                "whatever is waiting for them. You cannot command them: clear the road ahead. Enemy gunners will pick the trucks out first. " +
                "At least three must get through.",
                ObjectiveKind.Escort, 0, [],
                [
                    new(new(-5, 15), VehicleKind.T62),
                    new(new(-40, -2), VehicleKind.Bmp),
                    new(new(-55, 5), VehicleKind.T72),
                    new(new(-30, -8), VehicleKind.Bmp),
                ],
                Convoy: new(40, [new(24, 84), new(10, 50), new(-35, 10), new(-44, -10)], 4, 3),
                EnemyMines: false),
            new(
                "OPERATION SIGNAL",
                Mission.Briefings[2],
                ObjectiveKind.Capture, 3, Stations(2), StationGarrison(2)),
        ],
    ];

    /// <summary>Battles on <paramref name="island"/>.</summary>
    public static int BattleCount(int island) => Battles[island].Length;

    public static ZoneBattle Battle(int island, int battle) => Battles[island][battle];

    /// <summary>The battle for all an island's stations, which ends it.</summary>
    public static int FinalBattle(int island) => Battles[island].Length - 1;

    private static (string Name, Vector2 Position)[] Stations(int island) =>
        Mission.Sites(island).Select(s => (s.Name, s.Position)).ToArray();

    private static Garrison[] StationGarrison(int island) =>
        Mission.Sites(island)
            .SelectMany(site => StationGarrisons[island].Select((kind, n) => new Garrison(Mission.DefenderPost(site.Position, n), kind)))
            .ToArray();
}
