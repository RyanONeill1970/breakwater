namespace Breakwater;

public sealed record CommandBinding(string Id, string Label, Key DefaultKey);

/// <summary>Player preferences, persisted separately from campaign saves in <c>settings.json</c>.</summary>
public sealed class GameSettings
{
    /// <summary>Version 2 added Classic support commands; version 1 files gain available keys on load.</summary>
    public const int CurrentVersion = 2;
    public const float MinCameraSpeed = .25f;
    public const float MaxCameraSpeed = 2;

    public static readonly CommandBinding[] Commands =
    [
        new(CommandIds.Forward, "Drive forward", Key.W),
        new(CommandIds.Reverse, "Drive backward", Key.S),
        new(CommandIds.Left, "Turn left", Key.A),
        new(CommandIds.Right, "Turn right", Key.D),
        new(CommandIds.Fire, "Fire lead gun", Key.Space),
        new(CommandIds.Stop, "Stop group", Key.X),
        new(CommandIds.Regroup, "Regroup", Key.G),
        new(CommandIds.Hold, "Toggle automatic fire", Key.H),
        new(CommandIds.Group0, "Select Alpha", Key.Key1),
        new(CommandIds.Group1, "Select Bravo", Key.Key2),
        new(CommandIds.Group2, "Select Charlie", Key.Key3),
        new(CommandIds.Group3, "Select Delta", Key.Key4),
        new(CommandIds.View, "Cycle view (map, chase, sight, windows)", Key.Tab),
        new(CommandIds.Center, "Center camera", Key.F),
        new(CommandIds.PanUp, "Pan north", Key.Up),
        new(CommandIds.PanDown, "Pan south", Key.Down),
        new(CommandIds.PanLeft, "Pan west", Key.Left),
        new(CommandIds.PanRight, "Pan east", Key.Right),
        new(CommandIds.Pause, "Pause / resume", Key.P),
        new(CommandIds.Deploy, "Deploy force", Key.Enter),
        new(CommandIds.Restart, "Retry island", Key.R),
        new(CommandIds.Mute, "Toggle sound", Key.M),
        new(CommandIds.Save, "Save checkpoint", Key.F6),
        new(CommandIds.Slot, "Select save slot", Key.F7),
        new(CommandIds.Load, "Load checkpoint", Key.F9),
        new(CommandIds.Autosave, "Restore autosave", Key.F10),
        new(CommandIds.Smoke, "Fire smoke (Classic)", Key.C),
        new(CommandIds.Artillery, "Call artillery (Classic)", Key.B),
        new(CommandIds.Mine, "Lay mine (Classic)", Key.N),
    ];

    /// <summary>Commands added in version 2, which a version 1 file doesn't bind.</summary>
    private static readonly string[] AddedInVersion2 = [CommandIds.Smoke, CommandIds.Artillery, CommandIds.Mine];

    /// <summary>Nearby shots, hits and kills jolt the camera. Missing from older settings files, so it defaults on.</summary>
    public bool CameraShake { get; set; } = true;

    public float CameraSpeed { get; set; } = 1;
    public Dictionary<string, Key> Bindings { get; set; } = Commands.ToDictionary(c => c.Id, c => c.DefaultKey);
    public float CombatVolume { get; set; } = 1;
    public bool Fullscreen { get; set; }
    public float MasterVolume { get; set; } = 1;
    public bool Muted { get; set; }
    public float RadioVolume { get; set; } = 1;

    /// <summary>Draw the battlefield in low-resolution, few-colour pixels. Missing from older settings files, so it defaults off.</summary>
    public bool RetroLook { get; set; }

    /// <summary>Crunchy 8-bit sound on four channels. Missing from older settings files, so it defaults off.</summary>
    public bool RetroSound { get; set; }

    public int SaveSlot { get; set; }
    public int Version { get; set; } = CurrentVersion;

    public static string ActionName(string command) => "command_" + command;

    public static bool AllowedKey(Key key) =>
        Enum.IsDefined(key) && key != Key.None && key != Key.Unknown &&
        key is not (Key.Escape or Key.F1 or Key.Shift or Key.Ctrl or Key.Alt or Key.Meta or Key.Capslock or Key.Numlock or Key.Scrolllock);

    public static GameSettings Read(string path)
    {
        var result = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path)) ?? throw new FormatException("Empty settings file.");
        result.Migrate();
        result.Validate();
        return result;
    }

    public GameSettings Clone() => new()
    {
        Bindings = new(Bindings),
        CameraShake = CameraShake,
        CameraSpeed = CameraSpeed,
        CombatVolume = CombatVolume,
        Fullscreen = Fullscreen,
        MasterVolume = MasterVolume,
        Muted = Muted,
        RadioVolume = RadioVolume,
        RetroLook = RetroLook,
        RetroSound = RetroSound,
        SaveSlot = SaveSlot,
        Version = Version,
    };

    public string Hint(string command) => OS.GetKeycodeString(Bindings[command]);

    /// <summary>Assigns <paramref name="key"/>; if another command already has it, the two swap. Returns a message for the player.</summary>
    public string Rebind(string command, Key key)
    {
        if (!Bindings.ContainsKey(command) || !AllowedKey(key))
        {
            throw new ArgumentException("Choose a single key. F1, Escape and modifier keys are reserved.");
        }

        var other = Bindings.FirstOrDefault(p => p.Key != command && p.Value == key).Key;
        if (other != null)
        {
            Bindings[other] = Bindings[command];
        }

        Bindings[command] = key;
        return other == null ? "Binding changed." : $"Swapped with {Commands.Single(c => c.Id == other).Label.ToLowerInvariant()}.";
    }

    public void InstallBindings()
    {
        foreach (var command in Commands)
        {
            string action = ActionName(command.Id);
            if (!InputMap.HasAction(action))
            {
                InputMap.AddAction(action);
            }

            Input.ActionRelease(action);
            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = Bindings[command.Id] });
        }
    }

    /// <summary>Adds new commands while preserving existing bindings and preferences.</summary>
    private void Migrate()
    {
        if (Version != 1 || Bindings == null)
        {
            return;
        }

        foreach (var command in Commands.Where(c => AddedInVersion2.Contains(c.Id) && !Bindings.ContainsKey(c.Id)))
        {
            // Reserve the other commands' defaults so a fallback does not take their preferred key.
            Key key = command.DefaultKey;
            if (Bindings.ContainsValue(key))
            {
                key = Enumerable.Range((int)Key.A, (int)Key.Z - (int)Key.A + 1)
                    .Select(k => (Key)k)
                    .Concat(Enum.GetValues<Key>())
                    .FirstOrDefault(k => AllowedKey(k) && !Bindings.ContainsValue(k) && !Commands.Any(c => c.DefaultKey == k));
            }

            Bindings[command.Id] = key;
        }

        Version = CurrentVersion;
    }

    public void Validate()
    {
        static bool Volume(float value) => float.IsFinite(value) && value >= 0 && value <= 1;
        if (Version != CurrentVersion || !Volume(MasterVolume) || !Volume(CombatVolume) || !Volume(RadioVolume) ||
            !float.IsFinite(CameraSpeed) || CameraSpeed < MinCameraSpeed || CameraSpeed > MaxCameraSpeed ||
            SaveSlot < 0 || SaveSlot >= CampaignStorage.SlotCount || Bindings == null ||
            Bindings.Count != Commands.Length || Commands.Any(c => !Bindings.ContainsKey(c.Id)) ||
            Bindings.Values.Any(k => !AllowedKey(k)) || Bindings.Values.Distinct().Count() != Commands.Length)
        {
            throw new FormatException("Invalid settings. Defaults are available from the settings screen.");
        }
    }

    public void Write(string path)
    {
        Validate();
        CampaignStorage.WriteAtomically(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
