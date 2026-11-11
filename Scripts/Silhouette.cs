namespace Breakwater;

/// <summary>
/// How a vehicle kind's model differs from the basic tank: a taller hull for carriers, turret width,
/// gun length in metres, a missile launcher, a Soviet-style domed turret, armoured side skirts, and
/// road wheels in place of tracks. Enough to tell the Classic types apart at a glance.
/// </summary>
/// <param name="Light">Painted as a light vehicle rather than a tank.</param>
/// <param name="Deck">Extra hull height, raising the turret with it.</param>
/// <param name="Turret">Turret width.</param>
/// <param name="Barrel">Gun length.</param>
/// <param name="Launcher">Carries a missile launcher on the turret.</param>
/// <param name="Round">A low, rounded turret, as on Soviet designs, rather than a flat-sided one.</param>
/// <param name="Skirts">Armoured skirts hide the upper run of the tracks.</param>
/// <param name="Wheels">Wheels per side on a wheeled vehicle; zero for one on tracks.</param>
public sealed record Silhouette(bool Light, float Deck, float Turret, float Barrel, bool Launcher, bool Round = false, bool Skirts = false, int Wheels = 0)
{
    private static readonly Silhouette Tank = new(false, 0, 1.9f, 3.1f, false, Skirts: true);
    private static readonly Silhouette SovietTank = new(false, 0, 1.9f, 3.1f, false, Round: true);
    private static readonly Silhouette Scout = new(false, 0, 1.3f, 3.1f, false);
    private static readonly Silhouette FightingVehicle = new(true, .35f, 1.4f, 1.6f, true, Skirts: true);
    private static readonly Silhouette SovietFightingVehicle = new(true, .35f, 1.4f, 1.6f, true, Round: true);
    private static readonly Silhouette Carrier = new(true, .6f, .9f, .9f, false);
    private static readonly Silhouette WheeledCarrier = new(true, .6f, .9f, .9f, false, Round: true, Wheels: 4);
    private static readonly Silhouette MissileCarrier = new(true, .6f, 1.1f, .9f, true);
    private static readonly Silhouette Truck = new(true, .9f, 2.1f, 0, false, Wheels: 3);

    public static Silhouette Of(VehicleKind kind) => kind switch
    {
        VehicleKind.Scout => Scout,
        VehicleKind.Bradley => FightingVehicle,
        VehicleKind.Bmp => SovietFightingVehicle,
        VehicleKind.M113 => Carrier,
        VehicleKind.Btr => WheeledCarrier,
        VehicleKind.Itv => MissileCarrier,
        VehicleKind.Truck => Truck,
        VehicleKind.T72 or VehicleKind.T62 or VehicleKind.Defender => SovietTank,
        _ => Tank,
    };
}
