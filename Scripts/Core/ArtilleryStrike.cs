namespace Breakwater.Core;

using System.Numerics;

/// <summary>An artillery mission in flight: where it falls, what it fires, when the next round lands and how many remain.</summary>
public sealed class ArtilleryStrike
{
    /// <summary>Called by the enemy rather than the player.</summary>
    public bool Enemy { get; set; }

    public ArtilleryShell Shell { get; set; }
    public float NextRound { get; set; }
    public int RoundsLeft { get; set; }
    public Vector2 Target { get; set; }
}
