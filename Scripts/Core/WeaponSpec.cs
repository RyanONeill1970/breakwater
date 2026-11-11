namespace Breakwater.Core;

/// <summary>
/// A Classic-mode ammunition type's performance. <paramref name="Power"/> is divided by the target's
/// armour (see <see cref="ClassicRules.Damage"/>); range is in world metres and reload in seconds.
/// </summary>
public sealed record WeaponSpec(string Name, float Power, float Range, float Reload, bool NeedsHalt)
{
    /// <summary>Seconds between machine-gun bursts: in effect continuous fire.</summary>
    public const float BurstSeconds = .25f;

    // Ranges are in real metres, scaled to the island.
    private static readonly WeaponSpec MachineGun = new("MG", 1, ClassicRules.Distance(480), BurstSeconds, false);
    private static readonly WeaponSpec Heat = new("HEAT", 120, ClassicRules.Distance(1900), 4.5f, false);
    private static readonly WeaponSpec Sabot = new("SABOT", 1000, ClassicRules.Distance(2500), 7.5f, false);
    private static readonly WeaponSpec Tow = new("TOW", 2100, ClassicRules.Distance(3400), 18, true);

    public static WeaponSpec For(Weapon weapon) => weapon switch
    {
        Weapon.Heat => Heat,
        Weapon.Sabot => Sabot,
        Weapon.Tow => Tow,
        _ => MachineGun,
    };
}
