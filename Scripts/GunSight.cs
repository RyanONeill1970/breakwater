namespace Breakwater;

/// <summary>
/// The gunner sight's optic, drawn over the first-person view: a round eyepiece, a crosshair with
/// range ticks, and readouts for magnification, rangefinder, the round in the breech and the reload.
/// Ranges read in reference metres, the same units as the weapon ranges in the briefing.
/// </summary>
public partial class GunSight : Control
{
    // Sizes in pixels. The eyepiece is a circle of this fraction of the screen height.
    private const float EyepieceFraction = .36f;
    private const int EyepieceSegments = 128;
    private const float BezelWidth = 10;
    private const float ShadeWidth = 2400;
    private const float CrosshairGap = 14, CrosshairLength = 150, TickSpacing = 24, TickLength = 7;
    private const int TickCount = 5;
    private const int ReadoutFontSize = 17, HintFontSize = 13;
    // The readouts sit in a column left of the eyepiece, below the top bar.
    private static readonly Vector2 ReadoutOrigin = new(40, 150);
    private const float LineHeight = 26, ReadoutWidth = 300;

    /// <summary>The crosshair has no upper arm, so nothing hides the target above the aim point.</summary>
    private static readonly Vector2[] CrosshairArms = [Vector2.Left, Vector2.Right, Vector2.Down];

    /// <summary>How long the gunner's call on a shot stays under the crosshair, in simulation seconds.</summary>
    private const float ResultSeconds = 2.5f;
    private const int ResultFontSize = 26;
    private const float ResultDrop = 70;

    private Simulation? sim;
    private (string Text, Color Color, float Time)? result;
    private Vehicle? gunner;
    private bool zoomed;

    internal (string Text, Color Color)? ShotCall => sim != null &&
        result is var (text, color, time) && sim.Elapsed - time is >= 0 and < ResultSeconds ? (text, color) : null;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Keeps the call on the latest shot from the gun in the sight: HIT, KILL or MISS.</summary>
    public void Report(CombatEvent evt, float time)
    {
        if (sim != null && gunner != null && evt.Shooter == gunner.Id && WorldOverlay.ShotResult(sim, evt) is var (text, color))
        {
            result = (text, color, time);
        }
    }

    /// <summary>Shows the sight for <paramref name="vehicle"/>, or hides it when null.</summary>
    public void Refresh(Simulation simulation, Vehicle? vehicle, bool magnified)
    {
        if (sim != simulation || gunner != vehicle)
        {
            result = null;
        }
        sim = simulation;
        gunner = vehicle;
        zoomed = magnified;
        Visible = vehicle != null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (sim == null || gunner == null)
        {
            return;
        }

        var center = Size / 2;
        float radius = Size.Y * EyepieceFraction;
        DrawArc(center, radius + ShadeWidth / 2, 0, Mathf.Tau, EyepieceSegments, Palette.Ui.SightShade, ShadeWidth);
        // The eyepiece's rim: a dark bezel ring with the reticle's thin circle inside it.
        DrawArc(center, radius + BezelWidth / 2, 0, Mathf.Tau, EyepieceSegments, Palette.Ui.PanelBottom with { A = 1 }, BezelWidth, true);
        DrawArc(center, radius + BezelWidth, 0, Mathf.Tau, EyepieceSegments, Palette.Ui.PanelBorder, 1, true);
        DrawArc(center, radius, 0, Mathf.Tau, EyepieceSegments, Palette.Ui.SightReticle, 2, true);

        var color = Palette.Ui.SightReticle;
        foreach (var direction in CrosshairArms)
        {
            DrawLine(center + direction * CrosshairGap, center + direction * CrosshairLength, color, 2, true);
        }

        for (int i = 1; i <= TickCount; i++)
        {
            float y = center.Y + CrosshairGap + i * TickSpacing;
            DrawLine(new(center.X - TickLength, y), new(center.X + TickLength, y), color, 1, true);
            DrawLine(new(center.X - i * TickSpacing - CrosshairGap, center.Y - TickLength), new(center.X - i * TickSpacing - CrosshairGap, center.Y + TickLength), color, 1, true);
            DrawLine(new(center.X + i * TickSpacing + CrosshairGap, center.Y - TickLength), new(center.X + i * TickSpacing + CrosshairGap, center.Y + TickLength), color, 1, true);
        }

        if (ShotCall is var (callText, callColor))
        {
            DrawString(UiFonts.Callout, center + new Vector2(-radius, ResultDrop), callText, HorizontalAlignment.Center, radius * 2, ResultFontSize, callColor);
        }

        var weapon = WeaponSpec.For(gunner.Loaded);
        var range = sim.RangeAlongGun(gunner);
        string reading = range is { } r ? $"RANGE {r.Distance * ClassicRules.MetresPerWorldMetre:#,0} m" : "RANGE ----";
        if (range is { } far && far.Distance > weapon.Range)
        {
            reading += "  (too far)";
        }

        string laidOn = range is { Vehicle: { } seen } && sim.Recon.IsVisible(seen)
            ? $"ON {seen.Spec.Name}{(seen.Alive ? "" : " wreck")}{(seen.Enemy ? "" : "  FRIENDLY")}"
            : range is { } ? "ON COVER" : "";

        string rounds = gunner.Loaded == Weapon.MachineGun ? "unlimited" : $"{gunner.Rounds[(int)gunner.Loaded]} left";
        string state = gunner.Reload > 0 ? $"LOADING {gunner.Reload:0.0}s" : !gunner.CanFire(gunner.Loaded) ? "EMPTY" :
            weapon.NeedsHalt && Math.Abs(gunner.Speed) > 0 ? "HALT TO FIRE" : "READY";
        string[] lines =
        [
            $"{gunner.Spec.Name}   SIGHT {(zoomed ? CameraRig.SightMagnification : 1):0}×",
            reading,
            laidOn,
            $"{weapon.Name}  {rounds}",
            state,
        ];
        for (int i = 0; i < lines.Length; i++)
        {
            var lineColor = i == lines.Length - 1 && gunner.Reload > 0 ? Palette.Ui.Muted : Palette.Ui.Readout;
            DrawString(UiFonts.Readout, ReadoutOrigin + new Vector2(0, i * LineHeight), lines[i], HorizontalAlignment.Left, ReadoutWidth, ReadoutFontSize, lineColor);
        }

        DrawMultilineString(UiFonts.Body, ReadoutOrigin + new Vector2(0, (lines.Length + 1) * LineHeight),
            "Mouse or turn keys traverse. Click or the fire key shoots. Right-click changes ammunition. The wheel zooms. The view key leaves the sight.",
            HorizontalAlignment.Left, ReadoutWidth, HintFontSize, -1, Palette.Ui.Muted);
    }
}
