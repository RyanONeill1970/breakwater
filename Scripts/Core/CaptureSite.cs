namespace Breakwater.Core;

using System.Numerics;

public sealed class CaptureSite(string name, Vector2 position)
{
    /// <summary>Seconds of uncontested presence needed to secure a site.</summary>
    public const float HoldSeconds = 12;

    /// <summary>A friendly vehicle within this many metres builds control.</summary>
    public const float FriendlyRadius = 16;

    /// <summary>A defender within this many metres contests or reverses control.</summary>
    public const float EnemyRadius = 20;

    /// <summary>Enemy presence reverses progress at this fraction of the capture rate.</summary>
    public const float DecayRate = .5f;

    public bool Contested { get; set; }
    public float Fraction => Progress / HoldSeconds;
    public string Name { get; } = name;
    public Vector2 Position { get; set; } = position;
    public float Progress { get; set; }
    public bool Secured => Progress >= HoldSeconds;

    /// <summary>Seconds the site has been continuously secured; the enemy counter-attacks once it passes a delay.</summary>
    public float SecuredFor { get; set; }
}
