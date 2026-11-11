namespace Breakwater;

/// <summary>
/// Modal settings screen. Edits a draft copy; <see cref="Apply"/> is only called on "Apply settings",
/// and returns an error message or null on success.
/// </summary>
public partial class SettingsPanel : Control
{
    /// <summary>Sliders show percentages; settings store fractions.</summary>
    private const double Percent = 100;

    private const double CameraSpeedStep = 5;

    private readonly Dictionary<string, Button> bindingButtons = [];

    // Built in _Ready.
    private HSlider master = null!, combat = null!, radio = null!, speed = null!;
    private CheckButton mute = null!, fullscreen = null!, retro = null!, retroSound = null!, shake = null!;
    private Label notice = null!;

    private GameSettings draft = new();
    private string? capturing;

    public Func<GameSettings, string?>? Apply { get; set; }

    public Action? Closed { get; set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        var shade = new ColorRect { Color = Palette.Ui.SettingsShade };
        AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new(1000, 0) };
        center.AddChild(panel);
        var margin = new MarginContainer();
        string[] sides = ["left", "right", "top", "bottom"];
        foreach (string side in sides)
        {
            margin.AddThemeConstantOverride("margin_" + side, 24);
        }

        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        margin.AddChild(column);
        LabelAt(column, "SETTINGS", 30);
        LabelAt(column, "Combat is paused. Apply saves your choices for future sessions.", 15);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 32);
        column.AddChild(row);
        BuildAudioAndDisplay(row);
        BuildKeyboard(row);

        notice = LabelAt(column, "", 15);
        notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        notice.CustomMinimumSize = new(900, 42);
        notice.Modulate = Palette.Ui.Accent;
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        column.AddChild(footer);
        ButtonAt(footer, "Restore defaults", RestoreDefaults);
        ButtonAt(footer, "Credits", () => notice.Text = Credits.Text);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        ButtonAt(footer, "Cancel", Close);
        ButtonAt(footer, "Apply settings", ApplyDraft);
    }

    public void Open(GameSettings settings)
    {
        draft = settings.Clone();
        capturing = null;
        RefreshDraft();
        notice.Text = "Choose a key to reassign it. If it is already used, the two commands swap keys.";
        Show();
    }

    public override void _Input(InputEvent evt)
    {
        if (!Visible || evt is not InputEventKey key)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        if (!key.Pressed || key.Echo)
        {
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            if (capturing == null)
            {
                Close();
            }
            else
            {
                capturing = null;
                RefreshKeys();
                notice.Text = "Key capture cancelled.";
            }
            return;
        }

        if (capturing == null)
        {
            if (key.Keycode == Key.F1)
            {
                Close();
            }

            return;
        }

        if (key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed)
        {
            notice.Text = "Use a single key without modifiers.";
            return;
        }

        try
        {
            notice.Text = draft.Rebind(capturing, key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
            capturing = null;
            RefreshKeys();
        }
        catch (ArgumentException ex)
        {
            notice.Text = ex.Message;
        }
    }

    private static Label LabelAt(Node parent, string text, int size)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(label);
        return label;
    }

    private static Button ButtonAt(Node parent, string text, Action callback)
    {
        var button = new Button { FocusMode = FocusModeEnum.None, Text = text };
        button.Pressed += callback;
        parent.AddChild(button);
        return button;
    }

    private static HSlider SliderAt(Node parent, string text, double min, double max, double step, string name)
    {
        var label = LabelAt(parent, text, 16);
        var slider = new HSlider { CustomMinimumSize = new(280, 24), FocusMode = FocusModeEnum.None, MaxValue = max, MinValue = min, Name = name, Step = step };
        parent.AddChild(slider);
        slider.ValueChanged += v => label.Text = $"{text}  {v:0}%";
        return slider;
    }

    private void BuildAudioAndDisplay(Node row)
    {
        var column = new VBoxContainer { CustomMinimumSize = new(300, 0) };
        column.AddThemeConstantOverride("separation", 12);
        row.AddChild(column);
        LabelAt(column, "AUDIO & DISPLAY", 18);
        master = SliderAt(column, "Master volume", 0, Percent, 1, nameof(GameSettings.MasterVolume));
        combat = SliderAt(column, "Combat volume", 0, Percent, 1, nameof(GameSettings.CombatVolume));
        radio = SliderAt(column, "Radio volume", 0, Percent, 1, nameof(GameSettings.RadioVolume));
        mute = new CheckButton { FocusMode = FocusModeEnum.None, Text = "Mute all sound" };
        column.AddChild(mute);
        fullscreen = new CheckButton { FocusMode = FocusModeEnum.None, Text = "Fullscreen" };
        column.AddChild(fullscreen);
        shake = new CheckButton { FocusMode = FocusModeEnum.None, Text = "Camera shake" };
        column.AddChild(shake);
        // The two retro options share a row, to keep the panel on screen.
        var retroRow = new HBoxContainer();
        column.AddChild(retroRow);
        retro = new CheckButton { FocusMode = FocusModeEnum.None, Name = "RetroLook", SizeFlagsHorizontal = SizeFlags.ExpandFill, Text = "Retro look", TooltipText = "Low-resolution pixels and a small palette, like an early-90s home computer." };
        retroRow.AddChild(retro);
        retroSound = new CheckButton { FocusMode = FocusModeEnum.None, Name = "RetroSound", SizeFlagsHorizontal = SizeFlags.ExpandFill, Text = "Retro sound", TooltipText = "Crunchy 8-bit samples on four channels, like an early-90s sound chip." };
        retroRow.AddChild(retroSound);
        speed = SliderAt(column, "Camera pan speed", GameSettings.MinCameraSpeed * Percent, GameSettings.MaxCameraSpeed * Percent, CameraSpeedStep, nameof(GameSettings.CameraSpeed));
        var help = LabelAt(column, "F1 opens settings. Escape cancels key\ncapture or closes. Shift queues moves.\nMouse controls stay fixed. Key names are\npositions on a US QWERTY keyboard.", 14);
        help.Modulate = Palette.Ui.Muted;
    }

    private void BuildKeyboard(Node row)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(column);
        LabelAt(column, "KEYBOARD — click a key to change it", 18);
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 500), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var keys = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        keys.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(keys);
        foreach (var command in GameSettings.Commands)
        {
            var line = new HBoxContainer();
            keys.AddChild(line);
            var label = LabelAt(line, command.Label, 16);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var button = ButtonAt(line, "", () => Capture(command.Id));
            button.Name = "Bind_" + command.Id;
            button.CustomMinimumSize = new(170, 34);
            bindingButtons.Add(command.Id, button);
        }
    }

    private void RestoreDefaults()
    {
        draft = new GameSettings { SaveSlot = draft.SaveSlot };
        capturing = null;
        RefreshDraft();
        notice.Text = "Defaults selected. Apply to save, or Cancel to keep your current settings.";
    }

    private void ApplyDraft()
    {
        if (capturing != null)
        {
            notice.Text = "Press a key or Escape before applying.";
            return;
        }

        draft.CameraShake = shake.ButtonPressed;
        draft.RetroLook = retro.ButtonPressed;
        draft.RetroSound = retroSound.ButtonPressed;
        draft.CameraSpeed = (float)(speed.Value / Percent);
        draft.CombatVolume = (float)(combat.Value / Percent);
        draft.Fullscreen = fullscreen.ButtonPressed;
        draft.MasterVolume = (float)(master.Value / Percent);
        draft.Muted = mute.ButtonPressed;
        draft.RadioVolume = (float)(radio.Value / Percent);
        string? error = Apply?.Invoke(draft.Clone());
        if (error != null)
        {
            notice.Text = error;
            return;
        }

        Close();
    }

    private void RefreshDraft()
    {
        combat.Value = draft.CombatVolume * Percent;
        shake.ButtonPressed = draft.CameraShake;
        retro.ButtonPressed = draft.RetroLook;
        retroSound.ButtonPressed = draft.RetroSound;
        fullscreen.ButtonPressed = draft.Fullscreen;
        master.Value = draft.MasterVolume * Percent;
        mute.ButtonPressed = draft.Muted;
        radio.Value = draft.RadioVolume * Percent;
        speed.Value = draft.CameraSpeed * Percent;
        RefreshKeys();
    }

    private void RefreshKeys()
    {
        foreach (var (command, button) in bindingButtons)
        {
            button.Text = command == capturing ? "Press a key…" : draft.Hint(command);
        }
    }

    private void Capture(string command)
    {
        capturing = command;
        RefreshKeys();
        notice.Text = "Press a single key. Escape cancels. F1 and modifier keys are reserved.";
    }

    private void Close()
    {
        capturing = null;
        Hide();
        Closed?.Invoke();
    }
}
