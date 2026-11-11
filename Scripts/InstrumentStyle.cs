namespace Breakwater;

/// <summary>
/// A panel, button or gauge drawn like a piece of vehicle equipment: a plate shaded from top to
/// bottom with clipped corners, a bevelled edge that stands proud or sits recessed, optional corner
/// brackets and screw heads, and, for gauge fills, a row of lit segments. Every edge is antialiased.
/// </summary>
public partial class InstrumentStyle : StyleBox
{
    // Sizes in pixels.
    private const float BracketLength = 10, BracketWidth = 2, BracketInset = 3;
    private const float ScrewRadius = 2.2f, ScrewSlot = 1.5f, ScrewInset = 7;
    private const float SegmentGap = 2;

    /// <summary>Line and bevel width in pixels.</summary>
    private const float Line = 1;

    /// <summary>The plate colour at the top edge and the bottom edge.</summary>
    public Color Top { get; set; }
    public Color Bottom { get; set; }

    /// <summary>The outer edge.</summary>
    public Color Border { get; set; }

    /// <summary>Corner brackets' colour; transparent draws none.</summary>
    public Color Brackets { get; set; } = Colors.Transparent;

    /// <summary>How far each corner is clipped, in pixels.</summary>
    public float Chamfer { get; set; }

    /// <summary>Recessed into the surface, like a display window or a pressed key, rather than standing proud.</summary>
    public bool Inset { get; set; }

    /// <summary>Screw heads in the corners.</summary>
    public bool Screws { get; set; }

    /// <summary>Draw as lit segments this many pixels wide (for gauge fills); zero draws a solid plate.</summary>
    public float Segment { get; set; }

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        if (Segment > 0)
        {
            DrawSegments(toCanvasItem, rect);
            return;
        }

        var outline = Outline(rect, Chamfer);
        var shades = outline.Select(p => Top.Lerp(Bottom, (p.Y - rect.Position.Y) / Math.Max(1, rect.Size.Y))).ToArray();
        RenderingServer.CanvasItemAddPolygon(toCanvasItem, outline, shades);

        // Bevel: light on the top and left edges, where light falls, and dark opposite; swapped when recessed.
        var inner = Outline(rect.Grow(-Line), Math.Max(0, Chamfer - Line));
        var light = Palette.Ui.BevelLight;
        var dark = Palette.Ui.BevelDark;
        Vector2[] upper = [inner[7], inner[0], inner[1], inner[2], inner[3]];
        var lower = inner[3..];
        RenderingServer.CanvasItemAddPolyline(toCanvasItem, upper, One(Inset ? dark : light), Line, true);
        RenderingServer.CanvasItemAddPolyline(toCanvasItem, lower, One(Inset ? light : dark), Line, true);
        Vector2[] closed = [.. outline, outline[0]];
        RenderingServer.CanvasItemAddPolyline(toCanvasItem, closed, One(Border), Line, true);

        if (Brackets.A > 0)
        {
            DrawBrackets(toCanvasItem, rect.Grow(-BracketInset));
        }

        if (Screws)
        {
            var at = rect.Grow(-ScrewInset);
            foreach (var corner in new[] { at.Position, at.Position + new Vector2(at.Size.X, 0), at.End, at.Position + new Vector2(0, at.Size.Y) })
            {
                RenderingServer.CanvasItemAddCircle(toCanvasItem, corner, ScrewRadius, Palette.Ui.Screw, true);
                RenderingServer.CanvasItemAddLine(toCanvasItem, corner - new Vector2(ScrewSlot, 0), corner + new Vector2(ScrewSlot, 0), Palette.Ui.BevelDark, Line, true);
            }
        }
    }

    /// <summary>The plate's outline, clockwise from the left end of the top-left chamfer: points 1–2 are the top edge, 5–6 the bottom.</summary>
    private static Vector2[] Outline(Rect2 r, float chamfer)
    {
        float c = Math.Min(chamfer, Math.Min(r.Size.X, r.Size.Y) / 2);
        var (a, b) = (r.Position, r.End);
        return
        [
            new(a.X, a.Y + c), new(a.X + c, a.Y), new(b.X - c, a.Y), new(b.X, a.Y + c),
            new(b.X, b.Y - c), new(b.X - c, b.Y), new(a.X + c, b.Y), new(a.X, b.Y - c),
        ];
    }

    private void DrawBrackets(Rid item, Rect2 r)
    {
        foreach (var (corner, dx, dy) in new[] { (r.Position, 1, 1), (new Vector2(r.End.X, r.Position.Y), -1, 1), (r.End, -1, -1), (new Vector2(r.Position.X, r.End.Y), 1, -1) })
        {
            Vector2[] arms = [corner + new Vector2(dx * BracketLength, 0), corner, corner + new Vector2(0, dy * BracketLength)];
            RenderingServer.CanvasItemAddPolyline(item, arms, One(Brackets), BracketWidth, true);
        }
    }

    /// <summary>A single colour for a whole polyline.</summary>
    private static Color[] One(Color color) => [color];

    private void DrawSegments(Rid item, Rect2 rect)
    {
        for (float x = rect.Position.X; x < rect.End.X; x += Segment + SegmentGap)
        {
            var block = new Rect2(x, rect.Position.Y, Math.Min(Segment, rect.End.X - x), rect.Size.Y);
            RenderingServer.CanvasItemAddRect(item, block, Top);
        }
    }
}
