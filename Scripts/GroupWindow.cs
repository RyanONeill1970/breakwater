namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>
/// One of the four group windows: a title bar with the group's name and mode buttons, over the
/// leader's gun sight (its own camera in the shared 3D world), the island map, or a status report.
/// Clicking a window selects its group; in the selected group's sight a click fires.
/// </summary>
/// <remarks>Godot creates nodes through a parameterless constructor, so the group and callbacks are properties.</remarks>
public partial class GroupWindow : PanelContainer
{
    // Sizes in pixels.
    private const float ReticleGap = 6, ReticleArm = 40;
    private const int FrameMargin = 6, ReadoutOutline = 4;

    /// <summary>The readout strip along the foot of the sight: its height and the text's inset from its left end.</summary>
    private const float StripHeight = 22, StripInset = 8;

    /// <summary>With the retro look, a window's sight renders at this fraction of its size.</summary>
    private const int RetroShrink = 4;
    private const int TitleFontSize = 14, ReadoutFontSize = 13;

    // Built in _Ready.
    private Label title = null!, status = null!;
    private SubViewportContainer sightFrame = null!;
    private SubViewport sightViewport = null!;
    private Camera3D sightCamera = null!;
    private Control reticle = null!;
    private AspectRatioContainer mapFrame = null!;
    private TacticalMap map = null!;
    private readonly Dictionary<WindowMode, Button> modeButtons = [];

    /// <summary>The window's own panel style, so the selected group's window can wear its colour.</summary>
    private readonly InstrumentStyle frame = new() { Border = Palette.Ui.PanelBorder, Bottom = Palette.Ui.PanelBottom, Chamfer = 6, Top = Palette.Ui.PanelTop };

    /// <summary>How long a shot's call stays in the sight, in simulation seconds.</summary>
    private const float ResultSeconds = 2.5f;
    private const int ResultFontSize = 16;
    private const float ResultDrop = 34;

    private Simulation? sim;
    private (string Text, Color Color, float Time)? result;
    private bool selected;
    private bool zoomed;

    internal (string Text, Color Color)? ShotCall => sim != null &&
        result is var (text, color, time) && sim.Elapsed - time is >= 0 and < ResultSeconds ? (text, color) : null;

    public int Group { get; set; }
    public WindowMode Mode { get; private set; } = WindowMode.Sight;

    public IBattleCommands? Commands { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        frame.SetContentMarginAll(FrameMargin);
        AddThemeStyleboxOverride("panel", frame);
        var column = new VBoxContainer();
        AddChild(column);

        var bar = new HBoxContainer();
        column.AddChild(bar);
        title = new Label { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontOverride("font", UiFonts.Heading);
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        bar.AddChild(title);
        foreach (var mode in Enum.GetValues<WindowMode>())
        {
            var button = new Button { FocusMode = FocusModeEnum.None, Text = mode.ToString().ToUpperInvariant(), ToggleMode = true };
            button.AddThemeFontSizeOverride("font_size", TitleFontSize - 2);
            button.Pressed += () =>
            {
                SetMode(mode);
                Commands?.SelectGroup(Group);
            };
            bar.AddChild(button);
            modeButtons[mode] = button;
        }

        var maximise = new Button { FocusMode = FocusModeEnum.None, Text = "⤢", TooltipText = "Show this window full screen" };
        maximise.Pressed += () => Commands?.Maximise(Group, Mode);
        bar.AddChild(maximise);

        var content = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(content);

        sightFrame = new SubViewportContainer { MouseFilter = MouseFilterEnum.Ignore, Stretch = true };
        content.AddChild(sightFrame);
        sightFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Shares the battle's 3D world, so it shows the same scene through its own camera.
        // Smaller views need less mesh detail; retain full resolution and antialiasing for aiming.
        sightViewport = new SubViewport { HandleInputLocally = false, MeshLodThreshold = 4, Msaa3D = Viewport.Msaa.Msaa4X, World3D = GetViewport().World3D };
        sightFrame.AddChild(sightViewport);
        sightCamera = new Camera3D { Current = true };
        sightViewport.AddChild(sightCamera);
        reticle = new Control { MouseFilter = MouseFilterEnum.Stop };
        content.AddChild(reticle);
        reticle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        reticle.Draw += DrawReticle;
        reticle.GuiInput += SightInput;

        // The map is drawn to its control's size, so it sits in a square.
        mapFrame = new AspectRatioContainer { MouseFilter = MouseFilterEnum.Ignore, Ratio = 1 };
        content.AddChild(mapFrame);
        mapFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        map = new TacticalMap { Selected = Group };
        mapFrame.AddChild(map);
        map.Click = MapClick;

        status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Stop };
        status.AddThemeFontSizeOverride("font_size", ReadoutFontSize + 1);
        content.AddChild(status);
        status.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        status.GuiInput += SelectOnClick;
        SetMode(Mode);
    }

    /// <summary>Keeps the call on the latest shot from any vehicle in this window's group: HIT, KILL or MISS.</summary>
    public void Report(CombatEvent evt, float time)
    {
        if (sim != null && sim.Vehicles.Any(v => v.Id == evt.Shooter && !v.Enemy && v.Group == Group) && WorldOverlay.ShotResult(sim, evt) is var (text, color))
        {
            result = (text, color, time);
        }
    }

    /// <summary>Redraws the window; with <paramref name="retro"/> its sight renders at a quarter resolution with square pixels.</summary>
    public void Refresh(Simulation simulation, int selectedGroup, bool retro)
    {
        if (sim != simulation)
        {
            result = null;
            zoomed = false;
        }
        sightFrame.StretchShrink = retro ? RetroShrink : 1;
        sightFrame.TextureFilter = retro ? TextureFilterEnum.Nearest : TextureFilterEnum.ParentNode;
        sim = simulation;
        selected = selectedGroup == Group;
        var leader = sim.Leader(Group);
        int count = sim.Group(Group).Count();
        title.Text = $"{Group + 1}  {Mission.GroupNames[Group]}  {count}/{Mission.VehiclesPerGroup}";
        title.AddThemeColorOverride("font_color", selected ? Palette.Groups[Group] : Palette.Ui.Muted);
        frame.Border = selected ? Palette.Groups[Group] : Palette.Ui.PanelBorder;
        frame.Brackets = selected ? Palette.Groups[Group] : Colors.Transparent;
        frame.EmitChanged();

        bool showSight = IsVisibleInTree() && Mode == WindowMode.Sight && leader != null;
        sightViewport.RenderTargetUpdateMode = showSight ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        sightCamera.Environment = null;
        if (showSight && leader != null)
        {
            CameraRig.AimSight(sightCamera, leader, zoomed, sim.Night);
        }

        reticle.QueueRedraw();
        map.Simulation = sim;
        map.Selected = Group;
        map.CameraPosition = leader?.Position ?? V2.Zero;
        map.QueueRedraw();
        if (Mode == WindowMode.Status)
        {
            status.Text = StatusText(sim);
        }
    }

    public void SetMode(WindowMode mode)
    {
        Mode = mode;
        foreach (var (m, button) in modeButtons)
        {
            button.SetPressedNoSignal(m == mode);
        }

        sightFrame.Visible = mode == WindowMode.Sight;
        reticle.Visible = mode == WindowMode.Sight;
        mapFrame.Visible = mode == WindowMode.Map;
        status.Visible = mode == WindowMode.Status;
    }

    private string StatusText(Simulation s)
    {
        var lines = s.Campaign.Roster.Where(r => Mission.GroupOf(r.Id) == Group).Select(r =>
        {
            var v = s.Vehicles.FirstOrDefault(v => !v.Enemy && v.Id == r.Id);
            return v is { Alive: true } ? $"{v.Spec.Name}  {v.Health:0}%  {CommandHud.Magazine(v)}  ·  {v.Status.Describe()}" : "Lost";
        });
        var leader = s.Leader(Group);
        string fire = s.Group(Group).Any() && s.Group(Group).All(v => v.HoldFire) ? "Holding fire" : "Weapons free";
        return $"{string.Join("\n", lines)}\n\n{fire}  ·  {leader?.Orders.Count ?? 0} orders";
    }

    private void DrawReticle()
    {
        if (sim?.Leader(Group) is not Vehicle leader)
        {
            reticle.DrawString(UiFonts.Heading, new Vector2(0, reticle.Size.Y / 2), "GROUP LOST", HorizontalAlignment.Center, reticle.Size.X, TitleFontSize, Palette.Ui.Alert);
            return;
        }

        var center = reticle.Size / 2;
        var color = Palette.Ui.SightReticle;
        reticle.DrawLine(center + new Vector2(ReticleGap, 0), center + new Vector2(ReticleArm, 0), color, 1, true);
        reticle.DrawLine(center - new Vector2(ReticleGap, 0), center - new Vector2(ReticleArm, 0), color, 1, true);
        reticle.DrawLine(center + new Vector2(0, ReticleGap), center + new Vector2(0, ReticleArm), color, 1, true);
        if (ShotCall is var (callText, callColor))
        {
            var callAt = center + new Vector2(-reticle.Size.X / 2, ResultDrop);
            reticle.DrawStringOutline(UiFonts.Callout, callAt, callText, HorizontalAlignment.Center, reticle.Size.X, ResultFontSize, ReadoutOutline, Palette.Ui.CalloutOutline);
            reticle.DrawString(UiFonts.Callout, callAt, callText, HorizontalAlignment.Center, reticle.Size.X, ResultFontSize, callColor);
        }

        var weapon = WeaponSpec.For(leader.Loaded);
        string state = leader.Reload > 0 ? $"{leader.Reload:0.0}s" : "READY";
        string readout = $"{leader.Spec.Name}  {weapon.Name}  {state}{(zoomed ? $"  {CameraRig.SightMagnification:0}×" : "")}";
        // A display strip along the foot of the optic, like the data line in a real sight.
        var strip = new Rect2(0, reticle.Size.Y - StripHeight, reticle.Size.X, StripHeight);
        reticle.DrawRect(strip, Palette.Ui.Display with { A = Palette.Ui.PanelTop.A });
        reticle.DrawLine(strip.Position, strip.Position + new Vector2(strip.Size.X, 0), Palette.Ui.PanelBorder, 1);
        var font = UiFonts.Readout;
        float baseline = strip.Position.Y + (StripHeight + font.GetAscent(ReadoutFontSize) - font.GetDescent(ReadoutFontSize)) / 2;
        reticle.DrawString(font, new Vector2(StripInset, baseline), readout.ToUpperInvariant(), HorizontalAlignment.Left, -1, ReadoutFontSize, Palette.Ui.Readout);
    }

    private void SightInput(InputEvent evt)
    {
        if (evt is not InputEventMouseButton { Pressed: true } mouse)
        {
            return;
        }

        switch (mouse.ButtonIndex)
        {
            case MouseButton.Left when selected:
                Commands?.FireGunner();
                break;
            case MouseButton.Left:
                Commands?.SelectGroup(Group);
                break;
            case MouseButton.Right:
                Commands?.SelectGroup(Group);
                Commands?.CycleAmmunition();
                break;
            case MouseButton.WheelUp:
                zoomed = true;
                break;
            case MouseButton.WheelDown:
                zoomed = false;
                break;
        }

        reticle.AcceptEvent();
    }

    private void SelectOnClick(InputEvent evt)
    {
        if (evt is InputEventMouseButton { Pressed: true })
        {
            Commands?.SelectGroup(Group);
        }
    }

    private void MapClick(V2 point, bool right)
    {
        Commands?.SelectGroup(Group);
        if (right)
        {
            Commands?.OrderGroup(Group, point);
        }
    }
}
