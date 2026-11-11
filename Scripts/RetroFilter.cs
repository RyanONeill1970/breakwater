namespace Breakwater;

/// <summary>
/// The optional retro look (see <c>Shaders/Retro.gdshader</c>): a full-screen rect that redraws the 3D
/// view at <see cref="Width"/> pixels across, keeping the window's shape. The HUD sits on a higher
/// layer and stays sharp.
/// </summary>
public partial class RetroFilter : CanvasLayer
{
    private const string ShaderPath = "res://Shaders/Retro.gdshader";

    /// <summary>Retro pixels across the screen, as on a 320-pixel-wide home computer display.</summary>
    private const float Width = 320;

    // Built in _Ready.
    private ColorRect rect = null!;
    private ShaderMaterial material = null!;

    public override void _Ready()
    {
        // Under the HUD's layer, over the 3D view.
        Layer = 0;
        material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        rect = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(rect);
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        GetViewport().SizeChanged += Resize;
        Resize();
    }

    private void Resize()
    {
        var size = GetViewport().GetVisibleRect().Size;
        material.SetShaderParameter("resolution", new Vector2(Width, Mathf.Round(Width * size.Y / size.X)));
    }
}
