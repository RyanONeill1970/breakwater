namespace Breakwater;

// Preferences: loading, the settings panel, and quick toggles (mute, save slot) that persist immediately.
public partial class Battle
{
    private GameSettings settings = new();
    // Created in _Ready via InitializeSettingsPanel.
    private SettingsPanel settingsPanel = null!;
    private RetroFilter retroFilter = null!;
    private bool pausedBeforeSettings;
    private string? settingsLoadWarning;

    // Read and driven by QaDriver.
    internal GameSettings Settings => settings;
    internal string? SettingsLoadWarning => settingsLoadWarning;
    internal SettingsPanel SettingsPanel => settingsPanel;

    public void OpenSettings()
    {
        if (settingsPanel.Visible)
        {
            return;
        }

        pausedBeforeSettings = sim.Paused;
        sim.Paused = true;
        middleDrag = false;
        settingsPanel.Open(settings);
    }

    public void ToggleMute()
    {
        settings.Muted = !settings.Muted;
        audio.Configure(settings);
        PersistQuickSettings();
    }

    /// <summary>1 while the command's key is held and settings are closed, else 0.</summary>
    private float Held(string command) => !settingsPanel.Visible && Input.IsActionPressed(GameSettings.ActionName(command)) ? 1 : 0;

    private void LoadSettings()
    {
        // Scripted checks start from defaults, except the one that verifies persistence across a restart.
        if (options.IsQa && !options.SettingsReloadTest)
        {
            return;
        }

        try
        {
            if (File.Exists(storage.SettingsPath))
            {
                settings = GameSettings.Read(storage.SettingsPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            settings = new();
            settingsLoadWarning = "Settings could not be loaded; using defaults. Open Settings [F1] to save new choices.";
        }
    }

    private void InitializeSettingsPanel()
    {
        settingsPanel = new SettingsPanel();
        hud.AddChild(settingsPanel);
        settingsPanel.Apply = ApplySettings;
        settingsPanel.Closed = () =>
        {
            sim.Paused = pausedBeforeSettings;
            middleDrag = false;
        };
        ApplyPreferences();
    }

    private string? ApplySettings(GameSettings updated)
    {
        try
        {
            updated.Write(storage.SettingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            return "Settings not saved: " + ex.Message;
        }

        settings = updated;
        settings.InstallBindings();
        ApplyPreferences();
        hud.InvalidateHints();
        hud.Notify("Settings saved.");
        return null;
    }

    private void ApplyPreferences()
    {
        hud.Settings = settings;
        saveSlot = settings.SaveSlot;
        audio.Configure(settings);
        retroFilter.Visible = settings.RetroLook || options.Retro;
        ApplyWindowMode(settings.Fullscreen);
    }

    /// <summary>
    /// Exclusive fullscreen takes over the display at its desktop resolution; Godot's plain Fullscreen is a
    /// borderless window that Windows still treats as one. Leaving it restores the windowed size.
    /// </summary>
    private static void ApplyWindowMode(bool fullscreen)
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        var wanted = fullscreen ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed;
        if (DisplayServer.WindowGetMode() != wanted)
        {
            DisplayServer.WindowSetMode(wanted);
        }
    }

    private void PersistQuickSettings()
    {
        try
        {
            settings.Write(storage.SettingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            hud.Notify("Preference changed for this session; could not save: " + ex.Message);
        }
    }
}
