namespace Breakwater;

/// <summary>
/// Where checkpoints, the autosave and preferences live. Runtime checks get their own folder
/// under <c>.godot/qa</c>, chosen once here, so they never touch the player's files.
/// </summary>
public sealed class CampaignStorage(bool qa)
{
    /// <summary>Manual checkpoint slots (F7 cycles between them).</summary>
    public const int SlotCount = 3;

    private const string QaFolder = "res://.godot/qa/";

    public string AutosavePath => Globalize(qa ? QaFolder + "autosave.json" : "user://campaign-autosave.json");

    public string SettingsPath => Globalize(qa ? QaFolder + "settings.json" : "user://settings.json");

    /// <summary>A scratch file for runtime checks.</summary>
    public static string QaPath(string file) => Globalize(QaFolder + file);

    /// <summary>Writes via a temporary file, so a crash mid-write never leaves a truncated save.</summary>
    public static void WriteAtomically(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, contents);
        File.Move(temporary, path, true);
    }

    public static void Save(Simulation sim, string path) => WriteAtomically(path, SaveGame.Write(sim));

    public static Simulation Load(string path) => SaveGame.Read(File.ReadAllText(path));

    public string SlotPath(int slot) => Globalize(
        qa ? $"{QaFolder}ui-checkpoint-{slot}.json" :
        slot == 0 ? "user://quicksave.json" : $"user://quicksave-{slot + 1}.json");

    private static string Globalize(string path) => ProjectSettings.GlobalizePath(path);
}
