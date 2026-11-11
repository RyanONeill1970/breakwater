namespace Breakwater.Core;

/// <summary>A Classic-mode ammunition type. The values index <see cref="Vehicle.Rounds"/>.</summary>
public enum Weapon
{
    /// <summary>Unlimited, short-ranged and useless against armour.</summary>
    MachineGun,

    /// <summary>High explosive anti-tank: deadly to carriers, weak against tanks.</summary>
    Heat,

    /// <summary>Armour-piercing sabot: the tank killer.</summary>
    Sabot,

    /// <summary>Guided missile: longest reach and heaviest hit, but slow to reload and only fired at a halt.</summary>
    Tow,
}
