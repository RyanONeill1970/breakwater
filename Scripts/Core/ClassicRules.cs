namespace Breakwater.Core;

/// <summary>
/// Classic-mode tuning. Distances and speeds are written in real-world units (metres, km/h); these
/// helpers scale them onto Breakwater's much smaller islands so that ranges keep their proportion
/// to the island.
/// </summary>
public static class ClassicRules
{
    /// <summary>
    /// Real metres per world metre. A reference battlefield is 6.4 km across and a Breakwater island
    /// is 184 m, so this keeps ranges in proportion to the island.
    /// </summary>
    public const float MetresPerWorldMetre = 32;

    /// <summary>World metres per second for each km/h of reference top speed (an M1 on a road matches a Modern main tank).</summary>
    public const float WorldSpeedPerKmh = 1f / 8;

    /// <summary>A hit scales its damage by a random factor between this and 1.</summary>
    public const float MinimumDamageRoll = .3f;

    /// <summary>A vehicle below this health, in percent, is a wreck even if a hit did not finish it outright.</summary>
    public const float DestroyedBelow = 1.5f;

    /// <summary>Armour from which a vehicle counts as a tank for ammunition choice (the T-62's).</summary>
    public const int TankArmor = 1000;

    /// <summary>Armour of the light carriers (M113, ITV, BTR).</summary>
    public const int LightArmor = 60;

    // Ammunition worth firing at each target class, best first. Machine guns are only turned on carriers.
    private static readonly Weapon[] AgainstTanks = [Weapon.Sabot, Weapon.Tow, Weapon.Heat];
    private static readonly Weapon[] AgainstFightingVehicles = [Weapon.Heat, Weapon.Sabot, Weapon.Tow];
    private static readonly Weapon[] AgainstCarriers = [Weapon.Heat, Weapon.MachineGun];

    /// <summary>Everyone sees and reports enemies within 2.4 km.</summary>
    public static readonly float SightRange = Distance(2400);

    /// <summary>
    /// At night the enemy, lacking thermal sights, sees this fraction of its daytime range. The
    /// player's thermal sights are unaffected, and a muzzle flash shows up at full range.
    /// </summary>
    public const float NightEnemySight = .4f;

    /// <summary>
    /// A garrison holds its fire until an enemy comes this close, so a battle never opens with the
    /// landing force already under fire; the player can still open the fight from farther out.
    /// </summary>
    public static readonly float EngageRange = Distance(1600);

    /// <summary>When one defender is hit, every defender within this distance opens fire too.</summary>
    public static readonly float AlertRadius = Distance(1000);

    /// <summary>A vehicle in concealing terrain is seen only this many times closer than usual.</summary>
    public const float ConcealmentFactor = 8;

    /// <summary>Firing gives a vehicle's position away: it loses concealment for this many seconds.</summary>
    public const float RevealSeconds = 15;

    /// <summary>Standard deviation, in radians, of where a shot lands against a still target in the open.</summary>
    public const float BaseSpread = .011f;

    // Who is laying the gun. The player's crews firing on their own are poor shots, the enemy's are
    // steady on prepared ground, and a gun the player aims through the sight is the best of all, so
    // taking the gunner's seat is what wins battles.
    public const float PlayerCrewSpread = 2.5f;
    public const float EnemyCrewSpread = 1.5f;
    public const float AimedSpread = .6f;

    /// <summary>Shots scatter this much wider when the shooter moves, and again when the target moves.</summary>
    public const float MovingSpread = 2;

    // Defenders re-think at a random interval in this range, in seconds.
    public const float ThinkMinSeconds = 5;
    public const float ThinkMaxSeconds = 10;

    /// <summary>Defenders stand and fight when a reported enemy is within this range; otherwise they return to their posts.</summary>
    public static readonly float ContactRange = Distance(2400);

    /// <summary>Converts a reference distance in metres to world metres.</summary>
    public static float Distance(float metres) => metres / MetresPerWorldMetre;

    /// <summary>Converts a reference top speed in km/h to world metres per second.</summary>
    public static float Speed(float kmh) => kmh * WorldSpeedPerKmh;

    /// <summary>
    /// Health lost to one hit: full health × power ÷ armour × <paramref name="roll"/>, where the roll is
    /// between <see cref="MinimumDamageRoll"/> and 1. More than full health means destroyed outright.
    /// </summary>
    public static float Damage(float power, float armor, float roll) => Vehicle.MaxHealth * power / armor * roll;

    /// <summary>
    /// Ammunition worth trying against <paramref name="target"/>, best first: sabot or missiles against
    /// tanks, HEAT against fighting vehicles, HEAT or the machine gun against light carriers.
    /// </summary>
    public static IReadOnlyList<Weapon> Preference(VehicleKind target) => VehicleSpec.For(target).Armor switch
    {
        >= TankArmor => AgainstTanks,
        > LightArmor => AgainstFightingVehicles,
        _ => AgainstCarriers,
    };
}
