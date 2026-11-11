namespace Breakwater.Core;

/// <summary>Which rule set a campaign plays by. The mode is chosen when a campaign starts and never changes.</summary>
public enum GameMode
{
    /// <summary>Breakwater's own rules: two friendly vehicle types, one gun each, open ground.</summary>
    Modern,

    /// <summary>
    /// Rules modelled on the classic early-90s island tank game: eight vehicle types, armour values,
    /// four ammunition types, terrain that slows and hides vehicles, and defenders that hold their ground.
    /// </summary>
    Classic,
}
