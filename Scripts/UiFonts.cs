namespace Breakwater;

/// <summary>
/// The interface's typefaces, taken from the system so no font files ship with the game: a
/// condensed DIN-style face for labels, the same face spaced out for stencilled panel headings, and
/// a monospaced face for instrument readouts. Each falls back through similar faces to Godot's own.
/// </summary>
public static class UiFonts
{
    // Font weights, on the usual 100–900 scale.
    private const int RegularWeight = 400, HeadingWeight = 600;

    /// <summary>Extra pixels between the letters of a heading, as on a stencilled placard.</summary>
    private const int HeadingSpacing = 2;

    private static readonly string[] Condensed = ["Bahnschrift", "DIN Alternate", "Roboto Condensed", "Arial Narrow", "Liberation Sans Narrow", "sans-serif"];
    private static readonly string[] Monospaced = ["Consolas", "Cascadia Mono", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "monospace"];

    /// <summary>Labels, buttons and body text.</summary>
    public static Font Body { get; } = System(Condensed, RegularWeight, false);

    /// <summary>Panel headings and titles.</summary>
    public static Font Heading { get; } = Spaced(System(Condensed, HeadingWeight, false));

    /// <summary>Clocks, rangefinders and other readouts, whose digits shouldn't jostle as they change.</summary>
    public static Font Readout { get; } = System(Monospaced, RegularWeight, false);

    /// <summary>
    /// Outlined text drawn over the battlefield. Drawn from a distance field, so the outline stays
    /// smooth at any size rather than stepping round each letter.
    /// </summary>
    public static Font Callout { get; } = System(Condensed, HeadingWeight, true);

    private static SystemFont System(string[] names, int weight, bool distanceField) => new()
    {
        Fallbacks = [ThemeDB.FallbackFont],
        FontNames = names,
        FontWeight = weight,
        Hinting = TextServer.Hinting.Light,
        MultichannelSignedDistanceField = distanceField,
    };

    private static FontVariation Spaced(Font font)
    {
        var spaced = new FontVariation { BaseFont = font };
        spaced.SetSpacing(TextServer.SpacingType.Glyph, HeadingSpacing);
        return spaced;
    }
}
