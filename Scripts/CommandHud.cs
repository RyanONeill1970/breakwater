namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>
/// The command HUD: top bar, task-force panel, objectives, minimap and command bar. The
/// briefing/outcome modal is in <c>CommandHud.Modal.cs</c>. Layout sizes are pixels at the
/// 1440 × 900 reference resolution, scaled by Godot's canvas stretch.
/// </summary>
public partial class CommandHud : Control
{
    private const int ScreenMargin = 20;
    private const int BaseFontSize = 16;
    // Clipped corners, in pixels, and the width of a gauge's lit segments.
    private const float PanelChamfer = 7, ButtonChamfer = 3, GaugeSegment = 5;

    /// <summary>A slider track's thickness in pixels.</summary>
    private const float SliderTrack = 6;

    private readonly Dictionary<Button, Shortcut> shortcuts = [];
    private readonly Button[] groups = new Button[Mission.GroupCount];
    private readonly ProgressBar[] groupHealth = new ProgressBar[Mission.GroupCount];
    private readonly Label[] siteLabels = new Label[Mission.SitesPerIsland];
    private readonly ProgressBar[] siteProgress = new ProgressBar[Mission.SitesPerIsland];

    // Built in _Ready, which runs when Battle adds the HUD, before its first Refresh.
    private Label clock = null!, controlsHelp = null!, detail = null!, message = null!, mode = null!, objectiveHint = null!, operation = null!;
    private Button combatSlotButton = null!, fire = null!, pause = null!, sound = null!;
    private Button artilleryButton = null!, mineButton = null!, smokeButton = null!;
    private VBoxContainer support = null!;
    private GunSight gunSight = null!;
    private GridContainer groupWindows = null!;
    private ColorRect windowsBackdrop = null!;
    private TacticalMap map = null!;
    private WorldOverlay overlay = null!;

    /// <summary>The task force, objectives and minimap panels, which the gunner sight clears away.</summary>
    private readonly List<Control> sidePanels = [];

    private bool hintsDirty = true;
    private Simulation? sim;

    public IBattleCommands? Commands { get; set; }

    public GameSettings Settings { get; set; } = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = BuildTheme();
        overlay = new WorldOverlay();
        AddChild(overlay);
        gunSight = new GunSight();
        AddChild(gunSight);
        BuildGroupWindows();
        BuildTopBar();
        BuildTaskForce();
        BuildObjectives();
        BuildMinimap();
        BuildCommandBar();
        BuildModal();
    }

    /// <summary>Key bindings changed: re-caption every shortcut and rebuild the modal text.</summary>
    public void InvalidateHints() => hintsDirty = true;

    public void Notify(string text)
    {
        message.Text = text;
        if (modal.Visible)
        {
            modalNotice.Text = text;
        }
    }

    public void Report(CombatEvent evt, float time)
    {
        overlay.Report(evt, time);
        map.Report(evt, time);
        gunSight.Report(evt, time);
        foreach (var window in groupWindows.GetChildren().OfType<GroupWindow>())
        {
            window.Report(evt, time);
        }
    }

    /// <summary>Sets what a group's window shows.</summary>
    public void SetWindowMode(int group, WindowMode mode) => groupWindows.GetChildren().OfType<GroupWindow>().ElementAt(group).SetMode(mode);

    /// <summary>What the selected group's window shows, in the four-window view.</summary>
    public WindowMode WindowModeOf(int group) => groupWindows.GetChildren().OfType<GroupWindow>().ElementAt(group).Mode;

    /// <summary>Redraws the HUD for <paramref name="view"/>; <paramref name="magnified"/> is the full-screen sight's zoom.</summary>
    public void Refresh(Simulation simulation, Camera3D camera, int group, CameraView view, bool magnified, bool muted, int saveSlot)
    {
        bool sight = view == CameraView.Sight;
        bool rebuildModal = sim != simulation || lastPhase != simulation.Phase || hintsDirty;
        if (sim != simulation)
        {
            message.Text = "";
        }
        sim = simulation;
        if (hintsDirty)
        {
            RefreshHints();
        }

        SetCaption(combatSlotButton, $"Slot {saveSlot + 1}");
        SetCaption(slotButton, $"Slot {saveSlot + 1}");
        SetCaption(pause, sim.Paused ? "Resume" : "Pause");
        SetCaption(sound, muted ? "Sound off" : "Sound on");
        bool holding = sim.Group(group).Any() && sim.Group(group).All(v => v.HoldFire || v.Waiting);
        SetCaption(fire, holding ? "Weapons free" : "Hold fire");

        operation.Text = sim.ZoneBattle is ZoneBattle battle
            ? $"{battle.Name}  /  ISLAND {sim.Campaign.Island + 1} OF {Mission.Count}, BATTLE {sim.Campaign.Battle + 1} OF {Mission.BattleCount(sim.Campaign)}"
            : $"{Mission.Names[sim.Campaign.Island]}  /  ISLAND {sim.Campaign.Island + 1} OF {Mission.Count}";
        mode.Text = sim.Paused ? "PAUSED / ORDERS OPEN" : view switch
        {
            CameraView.Tactical => "TACTICAL COMMAND",
            CameraView.Sight => "GUNNER SIGHT",
            CameraView.Windows => "GROUP WINDOWS",
            _ => "CHASE CAMERA",
        };
        gunSight.Refresh(sim, sight ? sim.Leader(group) : null, magnified);
        overlay.Visible = view is CameraView.Tactical or CameraView.Chase;
        foreach (var panel in sidePanels)
        {
            panel.Visible = overlay.Visible;
        }

        groupWindows.Visible = view == CameraView.Windows;
        windowsBackdrop.Visible = groupWindows.Visible;
        // Bind hidden windows too: their callouts must reset before the next battle reports a shot.
        foreach (var window in groupWindows.GetChildren().OfType<GroupWindow>())
        {
            window.Refresh(sim, group, Settings.RetroLook);
        }

        clock.Text = TimeSpan.FromSeconds(sim.Elapsed).ToString(@"mm\:ss");
        RefreshTaskForce(simulation, group);
        RefreshObjectives(simulation);
        string tip = sim.Classic
            ? $"{ConditionStatus(sim)}\nBounties {Campaign.Money(sim.Bounty, GameMode.Classic)}  ·  Fines {Campaign.Money(sim.Fines, GameMode.Classic)}"
            : "Scouts observe farther than tanks.";
        support.Visible = sim.Classic;
        if (sim.Classic)
        {
            SetCaption(smokeButton, $"Smoke ({sim.Group(group).Sum(v => v.Smoke)} left)");
            SetCaption(artilleryButton, $"Artillery ({sim.ArtilleryLeft} left)");
            SetCaption(mineButton, $"Lay mine ({sim.MinesLeft} left)");
        }

        string goal = sim.ZoneBattle?.Objective switch
        {
            ObjectiveKind.Destroy => "Knock down the ringed building.",
            ObjectiveKind.Clear => "Destroy every enemy vehicle.",
            ObjectiveKind.Ambush => "Stop the convoy before it leaves the island.",
            ObjectiveKind.Escort => "Clear the road for the supply trucks.",
            ObjectiveKind.Hold => $"Hold the site until {TimeSpan.FromSeconds(sim.ZoneBattle.HoldSeconds):mm\\:ss}: {TimeSpan.FromSeconds(Math.Max(0, sim.ZoneBattle.HoldSeconds - sim.Elapsed)):mm\\:ss} to go.",
            _ => $"Hold each station for {CaptureSite.HoldSeconds:0} seconds.",
        };
        objectiveHint.Text = $"{goal}\n{tip}\nHollow markers: last known contact.";

        map.Simulation = sim;
        map.Selected = group;
        map.CameraPosition = ViewCenter(camera);
        map.QueueRedraw();
        overlay.Track(sim, camera);
        if (rebuildModal)
        {
            RefreshModal(simulation);
        }
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFont = UiFonts.Body, DefaultFontSize = BaseFontSize };
        theme.SetColor("font_color", "Label", Palette.Ui.Ink);
        foreach (string state in (string[])["font_color", "font_hover_color", "font_focus_color"])
        {
            theme.SetColor(state, "Button", Palette.Ui.Ink);
        }

        theme.SetColor("font_pressed_color", "Button", Palette.Ui.Readout);
        theme.SetColor("font_hover_pressed_color", "Button", Palette.Ui.Readout);
        theme.SetColor("font_disabled_color", "Button", Palette.Ui.Muted);
        theme.SetStylebox("normal", "Button", Plate(Palette.Ui.ButtonTop, Palette.Ui.ButtonBottom, Palette.Ui.ButtonBorder, ButtonChamfer));
        theme.SetStylebox("hover", "Button", Plate(Palette.Ui.ButtonHoverTop, Palette.Ui.ButtonHoverBottom, Palette.Ui.Legend, ButtonChamfer));
        theme.SetStylebox("pressed", "Button", Plate(Palette.Ui.ButtonPressedTop, Palette.Ui.ButtonPressedBottom, Palette.Ui.ButtonBorder, ButtonChamfer, inset: true));
        theme.SetStylebox("hover_pressed", "Button", Plate(Palette.Ui.ButtonPressedTop, Palette.Ui.ButtonPressedBottom, Palette.Ui.Legend, ButtonChamfer, inset: true));
        theme.SetStylebox("disabled", "Button", Plate(Palette.Ui.PanelTop, Palette.Ui.PanelBottom, Palette.Ui.PanelBorder, ButtonChamfer, inset: true));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        var panel = Plate(Palette.Ui.PanelTop, Palette.Ui.PanelBottom, Palette.Ui.PanelBorder, PanelChamfer);
        panel.Brackets = Palette.Ui.Brackets;
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "TooltipPanel", Plate(Palette.Ui.PanelTop with { A = 1 }, Palette.Ui.PanelBottom with { A = 1 }, Palette.Ui.Legend, ButtonChamfer));
        theme.SetStylebox("background", "ProgressBar", Plate(Palette.Ui.Track, Palette.Ui.Track, Palette.Ui.PanelBorder, 0, inset: true, padding: 0));
        theme.SetStylebox("fill", "ProgressBar", new InstrumentStyle { Segment = GaugeSegment, Top = Palette.Ui.Accent });
        // Sliders: a recessed track, lit up to the grabber like a gauge.
        var track = Plate(Palette.Ui.Track, Palette.Ui.Track, Palette.Ui.PanelBorder, 0, inset: true, padding: 0);
        track.ContentMarginTop = SliderTrack / 2;
        track.ContentMarginBottom = SliderTrack / 2;
        theme.SetStylebox("slider", "HSlider", track);
        var lit = new InstrumentStyle { Segment = GaugeSegment, Top = Palette.Ui.Accent };
        theme.SetStylebox("grabber_area", "HSlider", lit);
        theme.SetStylebox("grabber_area_highlight", "HSlider", lit);
        return theme;
    }

    /// <summary>An instrument plate; <paramref name="inset"/> recesses it, as for a pressed key or a display window.</summary>
    private static InstrumentStyle Plate(Color top, Color bottom, Color border, float chamfer, bool inset = false, int padding = 12)
    {
        var style = new InstrumentStyle { Border = border, Bottom = bottom, Chamfer = chamfer, Inset = inset, Top = top };
        style.ContentMarginLeft = padding;
        style.ContentMarginRight = padding;
        style.ContentMarginTop = padding / 2f;
        style.ContentMarginBottom = padding / 2f;
        return style;
    }
    private static PanelContainer Panel(Node parent, LayoutPreset preset, float left, float top, float right, float bottom)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        parent.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(preset);
        panel.OffsetLeft = left;
        panel.OffsetTop = top;
        panel.OffsetRight = right;
        panel.OffsetBottom = bottom;
        return panel;
    }

    /// <summary>A stencilled panel heading.</summary>
    private static Label Heading(Node parent, string text, int size, Color? color = null)
    {
        var label = LabelAt(parent, text, size, color ?? Palette.Ui.Legend);
        label.AddThemeFontOverride("font", UiFonts.Heading);
        return label;
    }

    private static Label LabelAt(Node parent, string text, int size, Color? color = null)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore, Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue)
        {
            label.AddThemeColorOverride("font_color", color.Value);
        }

        parent.AddChild(label);
        return label;
    }

    private static V2 ViewCenter(Camera3D camera)
    {
        // Where the centre of the view meets the ground; off-map (NaN) when the chase camera looks at the horizon.
        var center = camera.GetViewport().GetVisibleRect().Size / 2;
        var ray = camera.ProjectRayNormal(center);
        var origin = camera.ProjectRayOrigin(center);
        var look = origin + ray * (-origin.Y / ray.Y);
        return new(look.X, look.Z);
    }

    /// <summary>Adds a button. With a <paramref name="command"/>, its caption shows that command's current key.</summary>
    private Button ButtonAt(Node parent, string label, Action action, string? command = null)
    {
        var button = new Button { FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        button.Pressed += action;
        parent.AddChild(button);
        if (command != null)
        {
            shortcuts[button] = new(label, command);
        }

        button.Text = Caption(label, command);
        return button;
    }

    private string Caption(string label, string? command) => command == null ? label : $"{label}  [{Settings.Hint(command)}]";

    private void SetCaption(Button button, string label)
    {
        if (shortcuts.TryGetValue(button, out var shortcut) && shortcut.Label != label)
        {
            shortcuts[button] = shortcut with { Label = label };
            button.Text = Caption(label, shortcut.Command);
        }
    }

    private void RefreshHints()
    {
        hintsDirty = false;
        foreach (var (button, shortcut) in shortcuts)
        {
            button.Text = Caption(shortcut.Label, shortcut.Command);
        }

        string H(string command) => Settings.Hint(command);
        controlsHelp.Text =
            $"RMB move · Shift+RMB queue · {H(CommandIds.Forward)}/{H(CommandIds.Left)}/{H(CommandIds.Reverse)}/{H(CommandIds.Right)} drive · {H(CommandIds.Fire)} fire\n" +
            $"Wheel zoom · {H(CommandIds.PanUp)}/{H(CommandIds.PanLeft)}/{H(CommandIds.PanDown)}/{H(CommandIds.PanRight)} / middle drag pan · {H(CommandIds.View)} camera · {H(CommandIds.Center)} center";
    }

    /// <summary>The four-window view: one window per group, between the top bar and the command bar.</summary>
    private void BuildGroupWindows()
    {
        // Covers the battlefield behind and between the windows.
        windowsBackdrop = new ColorRect { Color = Palette.Ui.ModalShade with { A = 1 }, MouseFilter = MouseFilterEnum.Stop, Visible = false };
        AddChild(windowsBackdrop);
        windowsBackdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        groupWindows = new GridContainer { Columns = 2, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        groupWindows.AddThemeConstantOverride("h_separation", 8);
        groupWindows.AddThemeConstantOverride("v_separation", 8);
        AddChild(groupWindows);
        groupWindows.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        groupWindows.OffsetLeft = ScreenMargin;
        groupWindows.OffsetTop = 108;
        groupWindows.OffsetRight = -ScreenMargin;
        groupWindows.OffsetBottom = -154;
        for (int g = 0; g < Mission.GroupCount; g++)
        {
            groupWindows.AddChild(new GroupWindow
            {
                Commands = Commands,
                Group = g,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            });
        }
    }

    private void BuildTopBar()
    {
        var top = Panel(this, LayoutPreset.TopWide, ScreenMargin, ScreenMargin, -ScreenMargin, 98);
        var row = new HBoxContainer();
        top.AddChild(row);
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(title);
        Heading(title, "BREAKWATER", 26, Palette.Ui.Ink);
        operation = LabelAt(title, "", 12, Palette.Ui.Muted);
        mode = Heading(row, "TACTICAL", 13, Palette.Ui.Readout);
        mode.CustomMinimumSize = new(170, 0);
        mode.HorizontalAlignment = HorizontalAlignment.Right;
        mode.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        // The mission clock, in a recessed display window.
        var display = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        display.AddThemeStyleboxOverride("panel", Plate(Palette.Ui.Display, Palette.Ui.Display, Palette.Ui.PanelBorder, ButtonChamfer, inset: true, padding: 14));
        row.AddChild(display);
        clock = LabelAt(display, "00:00", 24, Palette.Ui.Readout);
        clock.AddThemeFontOverride("font", UiFonts.Readout);
        ButtonAt(row, "Map / chase", () => Commands?.ToggleView());
        pause = ButtonAt(row, "Pause", () => Commands?.TogglePause(), CommandIds.Pause);
        ButtonAt(row, "Settings [F1]", () => Commands?.OpenSettings());
    }

    private void BuildTaskForce()
    {
        var panel = Panel(this, LayoutPreset.TopLeft, ScreenMargin, 118, 280, 648);
        sidePanels.Add(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        Heading(column, "TASK FORCE", 13);
        for (int i = 0; i < Mission.GroupCount; i++)
        {
            int g = i;
            groups[i] = ButtonAt(column, Mission.GroupNames[i], () => Commands?.SelectGroup(g));
            groups[i].CustomMinimumSize = new(228, 44);
            groupHealth[i] = new ProgressBar { CustomMinimumSize = new(0, 4), MaxValue = Mission.VehiclesPerGroup * Vehicle.MaxHealth, ShowPercentage = false };
            column.AddChild(groupHealth[i]);
        }

        Heading(column, "SELECTED GROUP", 12);
        detail = LabelAt(column, "", 14);
        detail.CustomMinimumSize = new(228, 138);
        ButtonAt(column, "Stop movement", () => Commands?.Stop(), CommandIds.Stop);
        ButtonAt(column, "Regroup on leader", () => Commands?.Regroup(), CommandIds.Regroup);
        fire = ButtonAt(column, "Hold fire", () => Commands?.ToggleFire(), CommandIds.Hold);
    }

    private void BuildObjectives()
    {
        var panel = Panel(this, LayoutPreset.TopRight, -306, 118, -ScreenMargin, 362);
        sidePanels.Add(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        Heading(column, "MISSION OBJECTIVES", 13);
        for (int i = 0; i < Mission.SitesPerIsland; i++)
        {
            siteLabels[i] = LabelAt(column, $"0{i + 1}", 14);
            siteProgress[i] = new ProgressBar { CustomMinimumSize = new(250, 7), MaxValue = CaptureSite.HoldSeconds, ShowPercentage = false };
            column.AddChild(siteProgress[i]);
        }
        objectiveHint = LabelAt(column, "", 13, Palette.Ui.Muted);

        // Classic support weapons.
        support = new VBoxContainer();
        column.AddChild(support);
        Heading(support, "SUPPORT", 13);
        smokeButton = ButtonAt(support, "Smoke", () => Commands?.Smoke(), CommandIds.Smoke);
        artilleryButton = ButtonAt(support, "Artillery", () => Commands?.Artillery(), CommandIds.Artillery);
        artilleryButton.TooltipText = "Each press picks a shell (high explosive, cluster, smoke) or cancels; then right-click the ground or map to fire.";
        mineButton = ButtonAt(support, "Lay mine", () => Commands?.LayMine(), CommandIds.Mine);
        mineButton.TooltipText = "The group leader drops a mine behind it. It arms after a few seconds and hurts anyone, including you.";
    }

    private void BuildMinimap()
    {
        var panel = Panel(this, LayoutPreset.BottomRight, -306, -350, -ScreenMargin, -40);
        sidePanels.Add(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        Heading(column, "ISLAND OVERVIEW  /  N ↑", 12);
        map = new TacticalMap { CustomMinimumSize = new(254, 254), SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(map);
        map.Click = (p, right) => Commands?.MapClick(p, right);
        LabelAt(column, "LMB focus  ·  RMB order", 12, Palette.Ui.Muted);
    }

    private void BuildCommandBar()
    {
        var panel = Panel(this, LayoutPreset.BottomLeft, ScreenMargin, -144, 830, -ScreenMargin);
        var column = new VBoxContainer();
        panel.AddChild(column);
        message = LabelAt(column, "Awaiting deployment.", 15, Palette.Ui.Accent);
        controlsHelp = LabelAt(column, "", 13, Palette.Ui.Muted);
        controlsHelp.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        controlsHelp.CustomMinimumSize = new(780, 40);
        var buttons = new HBoxContainer();
        column.AddChild(buttons);
        combatSlotButton = ButtonAt(buttons, "Slot 1", () => Commands?.CycleSaveSlot(), CommandIds.Slot);
        ButtonAt(buttons, "Quicksave", () => Commands?.Save(), CommandIds.Save);
        ButtonAt(buttons, "Load", () => Commands?.Load(), CommandIds.Load);
        ButtonAt(buttons, "Autosave", () => Commands?.LoadAutosave(), CommandIds.Autosave);
        sound = ButtonAt(buttons, "Sound on", () => Commands?.ToggleMute(), CommandIds.Mute);
        ButtonAt(buttons, "Restart", () => Commands?.Restart(), CommandIds.Restart);
    }

    private void RefreshTaskForce(Simulation s, int group)
    {
        for (int i = 0; i < Mission.GroupCount; i++)
        {
            int count = 0;
            float health = 0;
            foreach (var v in s.Group(i))
            {
                count++;
                health += v.Health;
            }

            string marker = i == group ? "› " : "";
            groups[i].Text = $"{marker}{Settings.Hint(CommandIds.SelectGroup(i))}  {Mission.GroupNames[i]}      {count}/{Mission.VehiclesPerGroup}";
            groups[i].Modulate = i == group ? Palette.Groups[i] : Colors.White;
            groupHealth[i].Value = health;
        }

        var lines = s.Campaign.Roster.Where(r => Mission.GroupOf(r.Id) == group).Select(r =>
        {
            var v = s.Vehicles.FirstOrDefault(v => !v.Enemy && v.Id == r.Id);
            return v is { Alive: true } ? $"{v.Spec.Name}  {v.Health:0}%  /  {Magazine(v)}" : "Lost — replace in briefing";
        });
        var leader = s.Leader(group);
        string loaded = leader is { Spec.Classic: true } ? WeaponSpec.For(leader.Loaded).Name + " " : "";
        string gun = leader == null ? "—" : !leader.Spec.Classic && leader.Ammo == 0 ? "EMPTY" : leader.Reload > 0 ? $"{loaded}{leader.Reload:0.0}s" : $"{loaded}READY";
        string status = leader?.Status.Describe() ?? "Group destroyed";
        detail.Text = $"{string.Join("\n", lines)}\n\n{status}  ·  {leader?.Orders.Count ?? 0} orders\nLead gun: {gun}";
    }

    /// <summary>How the battle stands against its condition, or the standing tip when it has none.</summary>
    private static string ConditionStatus(Simulation s) => s.Conditions switch
    {
        { MaxLosses: int most } => $"Losses {s.Losses} of {most} allowed",
        { TimeLimitSeconds: float limit } => $"Time left {TimeSpan.FromSeconds(Math.Max(0, limit - s.Elapsed)):mm\\:ss}",
        { SpareCivilians: true } => "Spare the civilian buildings",
        _ => "Jungle edges give the best cover.",
    };

    /// <summary>Rounds left: one count for a Modern gun, each ammunition type carried for a Classic vehicle.</summary>
    internal static string Magazine(Vehicle v) => !v.Spec.Classic
        ? $"{v.Ammo} rds"
        : string.Join(" ", Enum.GetValues<Weapon>()
            .Where(w => w != Weapon.MachineGun && v.Spec.Carries(w))
            .Select(w => $"{v.Rounds[(int)w]} {WeaponSpec.For(w).Name}")
            .DefaultIfEmpty("MG only"));

    private void RefreshObjectives(Simulation simulation)
    {
        for (int i = 0; i < siteLabels.Length; i++)
        {
            bool shown = i < simulation.Sites.Count;
            siteLabels[i].Visible = shown;
            siteProgress[i].Visible = shown;
            if (!shown)
            {
                continue;
            }

            var s = simulation.Sites[i];
            string state = s.Secured ? "SECURED" : s.Contested ? "CONTESTED" : $"{s.Fraction:0%} CONTROL";
            string threat = simulation.Threatened(i) ? "  ·  ENEMY INBOUND" : "";
            siteLabels[i].Text = $"0{i + 1}  {s.Name}\n{state}{threat}";
            siteProgress[i].MaxValue = CaptureSite.HoldSeconds;
            siteProgress[i].Value = s.Progress;
        }

        // Battles won another way show their objective in the first row.
        switch (simulation.ZoneBattle?.Objective)
        {
            case ObjectiveKind.Destroy:
                ShowObjectiveRow($"TARGET\nStanding at {simulation.TargetHealth:0}%", simulation.TargetHealth, Vehicle.MaxHealth);
                break;
            case ObjectiveKind.Ambush:
                var battle = simulation.ZoneBattle;
                int need = (int)MathF.Ceiling(battle.AmbushQuota * battle.ColumnVehicles);
                ShowObjectiveRow(
                    $"CONVOY\n{simulation.ColumnKills} of {battle.ColumnVehicles} destroyed, {need} needed  ·  {simulation.Escaped} escaped",
                    simulation.ColumnKills, need);
                break;
            case ObjectiveKind.Escort:
                var convoy = simulation.ZoneBattle.Convoy!;
                int alive = simulation.Trucks.Count(t => t.Alive);
                string when = simulation.ConvoyMoving
                    ? $"{simulation.Delivered} of {convoy.Trucks} delivered, {convoy.MustArrive} needed  ·  {convoy.Trucks - alive} lost"
                    : $"Moves out in {TimeSpan.FromSeconds(convoy.DepartsAt - simulation.Elapsed):mm\\:ss}";
                ShowObjectiveRow($"SUPPLY CONVOY\n{when}", simulation.Delivered, convoy.MustArrive);
                break;
            case ObjectiveKind.Clear:
                int total = simulation.Vehicles.Count(v => v.Enemy);
                int left = simulation.Vehicles.Count(v => v.Enemy && v.Alive);
                ShowObjectiveRow($"DESTROY EVERY ENEMY\n{left} of {total} remaining", total - left, total);
                break;
        }
    }

    private void ShowObjectiveRow(string text, float value, float max)
    {
        siteLabels[0].Visible = true;
        siteProgress[0].Visible = true;
        siteLabels[0].Text = text;
        siteProgress[0].MaxValue = max;
        siteProgress[0].Value = value;
    }

    /// <summary>A button caption that shows a command's current key after its label.</summary>
    private sealed record Shortcut(string Label, string Command);
}
