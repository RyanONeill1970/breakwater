namespace Breakwater.Core;

/// <summary>What a Classic battle is won by.</summary>
public enum ObjectiveKind
{
    /// <summary>Hold every capture site with no defenders nearby.</summary>
    Capture,

    /// <summary>Knock down one building: a radar, a fuel dump, a command post.</summary>
    Destroy,

    /// <summary>Destroy every enemy vehicle.</summary>
    Clear,

    /// <summary>Hold a site against enemy columns until time is up: it must be secured then.</summary>
    Hold,

    /// <summary>Destroy enough of an enemy convoy before it drives off the island.</summary>
    Ambush,

    /// <summary>See enough of a friendly supply convoy, which the player cannot command, to its destination.</summary>
    Escort,
}
