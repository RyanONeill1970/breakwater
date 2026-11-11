namespace Breakwater;

/// <summary>
/// Keyboard command identifiers. They are also the keys in <c>settings.json</c>, so the string
/// values must not change; code refers to them through these constants rather than as literals.
/// </summary>
public static class CommandIds
{
    public const string Forward = "forward";
    public const string Reverse = "reverse";
    public const string Left = "left";
    public const string Right = "right";
    public const string Fire = "fire";
    public const string Stop = "stop";
    public const string Regroup = "regroup";
    public const string Hold = "hold";
    public const string Group0 = "group0";
    public const string Group1 = "group1";
    public const string Group2 = "group2";
    public const string Group3 = "group3";
    public const string View = "view";
    public const string Center = "center";
    public const string PanUp = "pan_up";
    public const string PanDown = "pan_down";
    public const string PanLeft = "pan_left";
    public const string PanRight = "pan_right";
    public const string Pause = "pause";
    public const string Deploy = "deploy";
    public const string Restart = "restart";
    public const string Mute = "mute";
    public const string Save = "save";
    public const string Slot = "slot";
    public const string Load = "load";
    public const string Autosave = "autosave";

    // Classic support weapons (settings version 2).
    public const string Smoke = "smoke";
    public const string Artillery = "artillery";
    public const string Mine = "mine";

    private static readonly string[] Groups = [Group0, Group1, Group2, Group3];

    public static string SelectGroup(int group) => Groups[group];

    /// <summary>The group a select-group command picks, or −1 for any other command.</summary>
    public static int GroupOf(string? command) => Array.IndexOf(Groups, command);
}
