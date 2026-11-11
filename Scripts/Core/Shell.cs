namespace Breakwater.Core;

using System.Numerics;

public sealed class Shell
{
    /// <summary>Muzzle velocity in m/s. Shells fly straight; there is no gravity model.</summary>
    public const float Speed = 90;

    /// <summary>Metres a shell travels before it is discarded.</summary>
    public const float MaxRange = 75;

    public int Id { get; set; }

    /// <summary>Modern: health taken from the target before armour.</summary>
    public float Damage { get; set; }

    public Vector2 Direction { get; set; }
    public int Owner { get; set; }
    public Vector2 Position { get; set; }

    /// <summary>Metres left to fly. A Classic round fired beyond its weapon's range falls short.</summary>
    public float Remaining { get; set; } = MaxRange;

    /// <summary>Classic: the ammunition fired, which sets the damage; null for a Modern shell.</summary>
    public Weapon? Weapon { get; set; }
}
