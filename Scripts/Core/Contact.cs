namespace Breakwater.Core;

using System.Numerics;

/// <summary>A remembered sighting: where a vehicle was last observed, not where it is now.</summary>
public sealed class Contact
{
    public int Id { get; set; }

    public bool Destroyed { get; set; }
    public float LastSeen { get; set; }
    public Vector2 Position { get; set; }
}
