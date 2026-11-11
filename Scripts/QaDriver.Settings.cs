namespace Breakwater;

using V2 = System.Numerics.Vector2;

// Settings checks: key remapping, apply/cancel/defaults, modal isolation, audio gain, HUD hints,
// validation, and (in a second process) persistence across a restart.
public partial class QaDriver
{
    /// <summary>Minimum metres a held drive key must move the leader for the binding to count as working.</summary>
    private const float MovedDistance = .2f;

    private V2 settingsCheckPosition;
    private float settingsCheckTime;
    private string settingsCheckSaved = "";

    private SettingsPanel Panel => battle.SettingsPanel;
    private GameSettings Settings => battle.Settings;
    private string SettingsPath => battle.Storage.SettingsPath;
    private Vehicle Leader => Sim.Leader(0) ?? throw new InvalidOperationException("Alpha has no vehicles");

    private static void Hold(Key key, bool pressed) =>
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });

    private Button PanelButton(string text) =>
        Panel.FindChildren("*", nameof(Button), true, false).OfType<Button>().First(b => b.Text == text);

    private Button Binding(string command) =>
        Panel.FindChild("Bind_" + command, true, false) as Button ?? throw new InvalidOperationException("Binding control missing");

    private HSlider Slider(string name) =>
        Panel.FindChild(name, true, false) as HSlider ?? throw new InvalidOperationException($"Slider {name} missing");

    private void SettingsReloadCheck()
    {
        if (frame == 10)
        {
            Check(battle.SettingsLoadWarning == null, "Settings reload warning");
            Check(Settings.Bindings[CommandIds.Forward] == Key.S && Settings.Bindings[CommandIds.Reverse] == Key.W &&
                Settings.Bindings[CommandIds.Fire] == Key.Q && Settings.Bindings[CommandIds.Hold] == Key.J, "Persisted bindings");
            Check(Settings.MasterVolume == .4f && Settings.CombatVolume == .3f && Settings.RadioVolume == 0 && Settings.CameraSpeed == 1.5f, "Persisted sliders");
            Check(!Settings.CameraShake, "Persisted camera shake");
            Check(Settings.Muted && battle.Audio.Muted && battle.SaveSlot == 1, "Persisted mute and save slot");
            KeyPress(Key.Enter);
            settingsCheckPosition = Leader.Position;
            Hold(Key.S, true);
        }

        if (frame == 40)
        {
            Hold(Key.S, false);
            Check(V2.Distance(settingsCheckPosition, Leader.Position) > MovedDistance, "Held movement binding after restart");
            Pass("SETTINGS RELOAD PASS: a fresh process restored bindings, audio, camera speed and shake, mute and save slot; remapped driving works.");
        }
    }

    private void SettingsCheck()
    {
        switch (frame)
        {
            case 10:
                KeyPress(Key.F1);
                break;
            case 12:
                Check(Panel.Visible && Sim.Paused && Sim.Phase == MissionPhase.Briefing, "Settings opens from briefing");
                Click(Binding(CommandIds.Forward));
                break;
            case 14:
                KeyPress(Key.S);
                break;
            case 16:
                Check(Binding(CommandIds.Forward).Text == "S" && Binding(CommandIds.Reverse).Text == "W", "Conflicting keys swap");
                Click(Binding(CommandIds.Fire));
                break;
            case 18:
                KeyPress(Key.F1);
                Check(Binding(CommandIds.Fire).Text == "Press a key…", "Reserved key rejected");
                KeyPress(Key.Escape);
                break;
            case 20:
                Check(Panel.Visible && Binding(CommandIds.Fire).Text == "Space", "Escape cancels capture only");
                Click(Binding(CommandIds.Fire));
                break;
            case 22:
                KeyPress(Key.Q);
                Slider(nameof(GameSettings.MasterVolume)).Value = 40;
                Slider(nameof(GameSettings.CombatVolume)).Value = 30;
                Slider(nameof(GameSettings.RadioVolume)).Value = 0;
                Slider(nameof(GameSettings.CameraSpeed)).Value = 150;
                Click(PanelButton("Camera shake"));
                Click(Binding(CommandIds.Hold));
                break;
            case 23:
                KeyPress(Key.J);
                Click(PanelButton("Apply settings"));
                break;
            case 24:
                Check(!Panel.Visible && !Sim.Paused && Settings.Bindings[CommandIds.Fire] == Key.Q, "Settings applies and restores prior pause");
                Check(!Settings.CameraShake, "Camera shake toggle applies");
                Check(GameSettings.Read(SettingsPath).Bindings[CommandIds.Forward] == Key.S, "Settings written");
                Check(battle.Hud.FindChildren("*", nameof(Label), true, false).OfType<Label>().Any(l => l.Text.Contains("Q fire")), "HUD reflects remapping");
                var combatPlayer = battle.Audio.FindChildren("*", nameof(AudioStreamPlayer3D), true, false).OfType<AudioStreamPlayer3D>().First();
                Check(Math.Abs(combatPlayer.VolumeDb - (-13 + Mathf.LinearToDb(.4f * .3f))) < .01f, "Master and combat gain applied");
                settingsCheckSaved = File.ReadAllText(SettingsPath);
                KeyPress(Key.F1);
                Click(PanelButton("Restore defaults"));
                break;
            case 26:
                Click(PanelButton("Cancel"));
                Check(File.ReadAllText(SettingsPath) == settingsCheckSaved && Settings.Bindings[CommandIds.Fire] == Key.Q, "Cancel discards draft and preserves file");
                KeyPress(Key.Enter);
                break;
            case 28:
                Check(Sim.Phase == MissionPhase.Active, "Deploy still works");
                KeyPress(Key.H);
                Check(!Leader.HoldFire, "Old shortcut removed");
                KeyPress(Key.J);
                Check(Leader.HoldFire, "Remapped command activates");
                KeyPress(Key.F1);
                settingsCheckPosition = Leader.Position;
                settingsCheckTime = Sim.Elapsed;
                Hold(Key.S, true);
                KeyPress(Key.Key2);
                KeyPress(Key.F7);
                break;
            case 40:
                Check(Sim.Elapsed == settingsCheckTime && Leader.Position == settingsCheckPosition && battle.Selected == 0 && battle.SaveSlot == 0, "Settings blocks simulation and commands");
                Hold(Key.S, false);
                KeyPress(Key.Escape);
                break;
            case 42:
                Check(!Sim.Paused, "Closing settings resumes running combat");
                KeyPress(Key.P);
                KeyPress(Key.F1);
                break;
            case 44:
                KeyPress(Key.F1);
                Check(Sim.Paused, "Closing settings preserves existing pause");
                KeyPress(Key.P);
                Hold(Key.S, true);
                settingsCheckPosition = Leader.Position;
                break;
            case 74:
                Hold(Key.S, false);
                Check(V2.Distance(settingsCheckPosition, Leader.Position) > MovedDistance, "Remapped continuous movement");
                KeyPress(Key.M);
                KeyPress(Key.F7);
                break;
            case 76:
                Check(Settings.Muted && battle.Audio.Muted && battle.SaveSlot == 1, "Quick preferences updated");
                settingsCheckSaved = File.ReadAllText(SettingsPath);
                KeyPress(Key.F1);
                Click(PanelButton("Restore defaults"));
                Click(PanelButton("Apply settings"));
                break;
            case 78:
                Check(Settings.Bindings[CommandIds.Forward] == Key.W && Settings.Bindings[CommandIds.Fire] == Key.Space && !battle.Audio.Muted && Settings.MasterVolume == 1 && Settings.CameraShake, "Restore defaults applies");
                VerifyInvalidSettings();
                // Leave the remapped fixture in place for the settings-reload check that runs next.
                File.WriteAllText(SettingsPath, settingsCheckSaved);
                Pass("SETTINGS PASS: conflict swapping, reserved keys, capture cancellation, apply/cancel/defaults, modal isolation, movement, audio gain, HUD hints and validation.");
                break;
        }
    }

    private static void VerifyInvalidSettings()
    {
        string path = CampaignStorage.QaPath("invalid-settings.json");
        var duplicate = new GameSettings();
        duplicate.Bindings[CommandIds.Forward] = Key.S;
        var reserved = new GameSettings();
        reserved.Bindings[CommandIds.Fire] = Key.F1;
        var missing = new GameSettings();
        missing.Bindings.Remove(CommandIds.Forward);
        string[] invalid =
        [
            "{bad json",
            "null",
            JsonSerializer.Serialize(duplicate),
            JsonSerializer.Serialize(reserved),
            JsonSerializer.Serialize(missing),
            JsonSerializer.Serialize(new GameSettings { Version = 99 }),
            JsonSerializer.Serialize(new GameSettings { MasterVolume = 2 }),
            JsonSerializer.Serialize(new GameSettings { SaveSlot = 5 }),
        ];
        foreach (string json in invalid)
        {
            File.WriteAllText(path, json);
            bool rejected = false;
            try
            {
                GameSettings.Read(path);
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                rejected = true;
            }

            if (!rejected || File.ReadAllText(path) != json)
            {
                throw new InvalidOperationException("Invalid settings were accepted or changed.");
            }
        }

        // A version 1 file predates the support-weapon commands and gains them on their default keys.
        var legacy = new GameSettings { Version = 1 };
        legacy.Bindings.Remove(CommandIds.Smoke);
        legacy.Bindings.Remove(CommandIds.Artillery);
        legacy.Bindings.Remove(CommandIds.Mine);
        File.WriteAllText(path, JsonSerializer.Serialize(legacy));
        var migrated = GameSettings.Read(path);
        Check(migrated.Version == GameSettings.CurrentVersion && migrated.Bindings[CommandIds.Artillery] == Key.B, "Version 1 settings migrate");

        legacy.Bindings[CommandIds.Fire] = Key.C;
        legacy.Bindings[CommandIds.Hold] = Key.B;
        legacy.Bindings[CommandIds.Stop] = Key.N;
        legacy.MasterVolume = .4f;
        legacy.CameraSpeed = 1.5f;
        legacy.SaveSlot = 2;
        string original = JsonSerializer.Serialize(legacy);
        File.WriteAllText(path, original);
        migrated = GameSettings.Read(path);
        Check(legacy.Bindings.All(binding => migrated.Bindings[binding.Key] == binding.Value), "Migration preserves custom bindings when all new defaults conflict");
        Check(migrated.MasterVolume == .4f && migrated.CameraSpeed == 1.5f && migrated.SaveSlot == 2, "Migration preserves preferences");
        Check(File.ReadAllText(path) == original, "Reading legacy settings does not overwrite them");
        migrated.Write(path);
        var reloaded = GameSettings.Read(path);
        Check(migrated.Bindings.All(binding => reloaded.Bindings[binding.Key] == binding.Value), "Migrated bindings survive save and reload");
    }
}
