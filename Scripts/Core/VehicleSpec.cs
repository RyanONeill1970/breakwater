namespace Breakwater.Core;

/// <summary>
/// Per-kind performance. Speeds are m/s, turn rates rad/s, ranges metres, reload seconds.
/// </summary>
/// <remarks>
/// Modern kinds carry one gun, described by <see cref="Range"/>, <see cref="Reload"/>,
/// <see cref="Damage"/> and <see cref="Ammo"/>; their values are game balance choices. Classic kinds
/// have <see cref="Armor"/> and a <see cref="Magazine"/> of several ammunition types instead
/// (<see cref="WeaponSpec"/>), scaled to the island via
/// <see cref="ClassicRules"/>. For them, <see cref="Range"/> and <see cref="Reload"/> are the longest
/// of their weapons', and <see cref="Speed"/> is their top speed on a road.
/// </remarks>
public sealed record VehicleSpec(
    string Name,
    float Speed,
    float HullTurn,
    float TurretTurn,
    float Range,
    float SightRange,
    float Reload,
    float Damage,
    int Ammo)
{
    private static readonly VehicleSpec MainTank = new("M1 Main tank", 9, 1.25f, 1.65f, 55, 60, 3.1f, 40, 32);
    private static readonly VehicleSpec Scout = new("M8 Scout", 13, 1.8f, 2.4f, 46, 82, 2.4f, 23, 36);
    private static readonly VehicleSpec Defender = new("T-62", 7, 1.1f, 1.1f, 48, 60, 3.8f, 28, 40);

    // Classic kinds: name, top speed (km/h), turn rates, armour, and rounds of HEAT, sabot and missiles.
    private static readonly VehicleSpec Abrams = ClassicKind("M1 Abrams", 72, 1.25f, 1.65f, 2000, heat: 26, sabot: 32) with { SmokeRounds = 2 };
    private static readonly VehicleSpec Bradley = ClassicKind("M2 Bradley", 66, 1.4f, 1.9f, 750, tow: 8) with { SmokeRounds = 2 };
    private static readonly VehicleSpec M113 = ClassicKind("M113", 50, 1.4f, 1.9f, ClassicRules.LightArmor, tow: 5);
    private static readonly VehicleSpec Itv = ClassicKind("M901 ITV", 64, 1.4f, 1.9f, ClassicRules.LightArmor, tow: 14);
    private static readonly VehicleSpec T72 = ClassicKind("T-72", 60, 1.2f, 1.4f, 1350, heat: 44, sabot: 8) with { SmokeRounds = 2 };
    private static readonly VehicleSpec T62 = ClassicKind("T-62", 50, 1.1f, 1.1f, ClassicRules.TankArmor, heat: 32, sabot: 22) with { SmokeRounds = 2 };
    private static readonly VehicleSpec Bmp = ClassicKind("BMP-2", 80, 1.6f, 1.9f, 240, heat: 22, tow: 10);
    private static readonly VehicleSpec Btr = ClassicKind("BTR-60", 80, 1.6f, 1.9f, ClassicRules.LightArmor);
    private static readonly VehicleSpec Truck = ClassicKind("Supply truck", 70, 1.6f, 1.9f, ClassicRules.LightArmor / 2);

    /// <summary>Classic armour; zero for Modern kinds.</summary>
    public int Armor { get; private init; }

    /// <summary>Whether this kind plays by <see cref="GameMode.Classic"/> rules.</summary>
    public bool Classic => Armor > 0;

    /// <summary>
    /// Classic rounds carried when fully loaded, indexed by <see cref="Weapon"/>. The machine gun's
    /// entry is unused: every Classic vehicle has one, with unlimited ammunition. Empty for Modern kinds.
    /// </summary>
    public IReadOnlyList<int> Magazine { get; private init; } = [];

    /// <summary>Classic smoke grenades carried.</summary>
    public int SmokeRounds { get; private init; }

    // Returns a shared instance: this is read several times per vehicle per simulation substep.
    public static VehicleSpec For(VehicleKind kind) => kind switch
    {
        VehicleKind.Scout => Scout,
        VehicleKind.Defender => Defender,
        VehicleKind.Abrams => Abrams,
        VehicleKind.Bradley => Bradley,
        VehicleKind.M113 => M113,
        VehicleKind.Itv => Itv,
        VehicleKind.T72 => T72,
        VehicleKind.T62 => T62,
        VehicleKind.Bmp => Bmp,
        VehicleKind.Btr => Btr,
        VehicleKind.Truck => Truck,
        _ => MainTank,
    };

    /// <summary>Whether this kind carries <paramref name="weapon"/>. Every Classic kind has a machine gun.</summary>
    public bool Carries(Weapon weapon) => Classic && (weapon == Weapon.MachineGun || Magazine[(int)weapon] > 0);

    private static VehicleSpec ClassicKind(string name, float kmh, float hullTurn, float turretTurn, int armor, int heat = 0, int sabot = 0, int tow = 0)
    {
        int[] magazine = new int[Enum.GetValues<Weapon>().Length];
        magazine[(int)Weapon.Heat] = heat;
        magazine[(int)Weapon.Sabot] = sabot;
        magazine[(int)Weapon.Tow] = tow;
        var weapons = Enum.GetValues<Weapon>().Where(w => w == Weapon.MachineGun || magazine[(int)w] > 0).Select(WeaponSpec.For).ToArray();
        return new(name, ClassicRules.Speed(kmh), hullTurn, turretTurn, weapons.Max(w => w.Range), ClassicRules.SightRange, weapons.Max(w => w.Reload), 0, 0)
        {
            Armor = armor,
            Magazine = magazine,
        };
    }
}
