namespace Breakwater;

/// <summary>
/// The third-party art in <c>assets/</c>, credited as its licences require: the vehicle models are
/// CC BY 4.0, which asks for the author's name wherever the work is shared; the sky and surface
/// photographs are CC0 and credited as a courtesy. <c>assets/CREDITS.md</c> carries the same list
/// with links, and the settings screen shows <see cref="Text"/>.
/// </summary>
public static class Credits
{
    private static readonly (string Work, string Author)[] Models =
    [
        ("Low Poly Abrams Free Model", "Lasha-Georgy Bajadze"),
        ("Low Poly M2 Bradley Free Model", "Lasha-Georgy Bajadze"),
        ("M113A1", "Metehan"),
        ("T-72B1", "42manako"),
        ("T-62MD", "42manako"),
        ("BMP-2 (low-poly)", "ScurvyWoof"),
        ("SPW-60PB (East Germany)", "42manako"),
        ("M35 Truck", "ZeroFrost"),
    ];

    public static string Text =>
        "Vehicle models from Sketchfab, CC BY 4.0: " + string.Join("; ", Models.Select(m => $"\"{m.Work}\" by {m.Author}")) +
        ". Skies and surface textures: Poly Haven (CC0).";
}
