namespace Breakwater;

/// <summary>Command-line switches for runtime checks and visual previews (arguments after <c>--</c>).</summary>
public sealed class LaunchOptions
{
    private const int DefaultScreenshotFrame = 40;

    /// <summary>Stage a close-range skirmish in view of the camera, for effect screenshots.</summary>
    public bool BattlePreview { get; private init; }

    public bool CampaignTest { get; private init; }

    /// <summary>Start (or preview) a Classic-rules campaign instead of a Modern one.</summary>
    public bool Classic { get; private init; }

    public bool ClassicTest { get; private init; }

    /// <summary>Put groups on the objectives mid-capture, one contested and one about to be secured, for screenshots.</summary>
    public bool CapturePreview { get; private init; }
    public bool ChasePreview { get; private init; }

    /// <summary>Hold the first objective and have the enemy counter-attack it and land reinforcements, for screenshots.</summary>
    public bool CounterAttackPreview { get; private init; }

    /// <summary>Classic battle within the island to preview, if given (with <c>--classic</c>).</summary>
    public int? Battle { get; private init; }

    /// <summary>Island to preview with a fresh force, if given.</summary>
    public int? Island { get; private init; }

    /// <summary>Line up every vehicle model, turrets turned, for checking the models' fit.</summary>
    public bool LineupPreview { get; private init; }

    /// <summary>With <c>--lineup-preview</c>, a vehicle kind to look at close up, turret turned.</summary>
    public VehicleKind? LineupFocus { get; private init; }

    public bool Preview { get; private init; }

    /// <summary>Force the retro look on, for screenshots, whatever the saved settings say.</summary>
    public bool Retro { get; private init; }

    public int ScreenshotFrame { get; private init; } = DefaultScreenshotFrame;
    public string? ScreenshotPath { get; private init; }
    public bool SettingsPreview { get; private init; }

    /// <summary>Look through Charlie's gunner sight (Classic mode; pair with <c>--battle-preview</c> for targets).</summary>
    public bool SightPreview { get; private init; }

    /// <summary>Show the four group windows (Classic mode; pair with <c>--battle-preview</c> for action).</summary>
    public bool WindowsPreview { get; private init; }

    /// <summary>Stage smoke, a mine and fire missions from both sides (Classic mode), for screenshots.</summary>
    public bool SupportPreview { get; private init; }
    public bool SettingsReloadTest { get; private init; }
    public bool SettingsTest { get; private init; }
    public bool SmokeTest { get; private init; }
    public bool SceneryTest { get; private init; }
    public bool UiTest { get; private init; }
    public bool ViewStateTest { get; private init; }

    /// <summary>Any scripted check or preview: these use scratch storage and never the player's saves.</summary>
    public bool IsQa => SmokeTest || SceneryTest || UiTest || ViewStateTest || CampaignTest || ClassicTest || SettingsTest || SettingsReloadTest ||
        Preview || LineupPreview || BattlePreview || CapturePreview || ChasePreview || CounterAttackPreview || SettingsPreview || SightPreview || WindowsPreview || SupportPreview || ScreenshotPath != null;

    public static LaunchOptions Parse(string[] args)
    {
        string? Value(string prefix) => args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

        int? island = int.TryParse(Value("--island="), out int i) && i >= 0 && i < Mission.Count ? i : null;
        int? battle = int.TryParse(Value("--battle="), out int b) && b >= 0 ? b : null;
        return new LaunchOptions
        {
            Battle = battle,
            BattlePreview = args.Contains("--battle-preview"),
            CampaignTest = args.Contains("--campaign-test"),
            Classic = args.Contains("--classic"),
            ClassicTest = args.Contains("--classic-test"),
            CapturePreview = args.Contains("--capture-preview"),
            ChasePreview = args.Contains("--chase-preview"),
            CounterAttackPreview = args.Contains("--counter-attack-preview"),
            Island = island,
            LineupFocus = Enum.TryParse(Value("--focus="), true, out VehicleKind focus) ? focus : null,
            LineupPreview = args.Contains("--lineup-preview"),
            Preview = args.Contains("--preview"),
            Retro = args.Contains("--retro"),
            ScreenshotFrame = int.TryParse(Value("--screenshot-frame="), out int frame) ? frame : DefaultScreenshotFrame,
            ScreenshotPath = Value("--screenshot="),
            SettingsPreview = args.Contains("--settings-preview"),
            SightPreview = args.Contains("--sight-preview"),
            SupportPreview = args.Contains("--support-preview"),
            WindowsPreview = args.Contains("--windows-preview"),
            SettingsReloadTest = args.Contains("--settings-reload-test"),
            SettingsTest = args.Contains("--settings-test"),
            SmokeTest = args.Contains("--smoke-test"),
            SceneryTest = args.Contains("--scenery-test"),
            UiTest = args.Contains("--ui-test"),
            ViewStateTest = args.Contains("--view-state-test"),
        };
    }
}
