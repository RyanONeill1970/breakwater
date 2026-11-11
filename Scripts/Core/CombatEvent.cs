namespace Breakwater.Core;

using System.Numerics;

/// <summary>Something the presentation should show or play: a shot, hit, kill, order or report.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Position">Where it happened.</param>
/// <param name="Message">Radio text for the player, or empty.</param>
/// <param name="UnitId">The vehicle concerned, or −1.</param>
/// <param name="PlayerVisible">False when fog of war hides it from the player.</param>
/// <param name="Armor">For a hit on a live vehicle, the face the shell struck; otherwise null.</param>
/// <param name="Damage">For a hit, the health it took; otherwise 0.</param>
/// <param name="Shooter">For a shell's impact or miss, the vehicle that fired it, or −1.</param>
public sealed record CombatEvent(
    CombatEventKind Kind,
    Vector2 Position,
    string Message = "",
    int UnitId = -1,
    bool PlayerVisible = true,
    ArmorFacing? Armor = null,
    float Damage = 0,
    int Shooter = -1);
