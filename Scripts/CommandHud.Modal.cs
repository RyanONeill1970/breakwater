namespace Breakwater;

// The briefing / outcome modal: campaign progress, deployment logistics and the next action.
public partial class CommandHud
{
    private readonly Label[] rosterLabels = new Label[Mission.GroupCount];
    private readonly Button[] refitButtons = new Button[Mission.GroupCount];
    private readonly Button[] repairButtons = new Button[Mission.GroupCount];
    private readonly Button[] replaceButtons = new Button[Mission.GroupCount];

    // Built in BuildModal, called from _Ready.
    private Control modal = null!;
    private Label budget = null!, campaignStrip = null!, modalBody = null!, modalNotice = null!, modalTitle = null!;
    private Button modalAction = null!, modeButton = null!, slotButton = null!;
    private VBoxContainer deployment = null!;
    private ZoneMap zoneMap = null!;

    /// <summary>The phase the modal was last built for; null forces a rebuild.</summary>
    private MissionPhase? lastPhase;

    private void BuildModal()
    {
        modal = new Control();
        AddChild(modal);
        modal.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = Palette.Ui.ModalShade };
        modal.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        modal.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new(860, 0) };
        center.AddChild(panel);
        var margin = new MarginContainer();
        string[] sides = ["left", "right", "top", "bottom"];
        foreach (string side in sides)
        {
            margin.AddThemeConstantOverride("margin_" + side, 26);
        }

        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        margin.AddChild(column);

        var heading = new HBoxContainer();
        column.AddChild(heading);
        campaignStrip = LabelAt(heading, "", 13, Palette.Ui.Accent);
        campaignStrip.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ButtonAt(heading, "Settings [F1]", () => Commands?.OpenSettings());
        ButtonAt(heading, "Exit game", () => Commands?.Quit());
        modalTitle = LabelAt(column, "", 34);
        var brief = new HBoxContainer();
        brief.AddThemeConstantOverride("separation", 18);
        column.AddChild(brief);
        modalBody = LabelAt(brief, "", 16);
        modalBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.CustomMinimumSize = new(780, 0);
        modalBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        zoneMap = new ZoneMap { CustomMinimumSize = new(150, 150), SizeFlagsVertical = SizeFlags.ShrinkBegin };
        brief.AddChild(zoneMap);

        deployment = new VBoxContainer();
        deployment.AddThemeConstantOverride("separation", 8);
        column.AddChild(deployment);
        budget = LabelAt(deployment, "", 16, Palette.Ui.Accent);
        LabelAt(deployment, "Refit preserves damage. Repairs service survivors; replacement buys one lost vehicle.", 13, Palette.Ui.Muted);
        for (int i = 0; i < Mission.GroupCount; i++)
        {
            int g = i;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            deployment.AddChild(row);
            rosterLabels[i] = LabelAt(row, "", 14);
            rosterLabels[i].CustomMinimumSize = new(190, 0);
            refitButtons[i] = ButtonAt(row, "Refit", () => Commands?.Configure(g, DeployAction.Refit));
            refitButtons[i].CustomMinimumSize = new(220, 0);
            repairButtons[i] = ButtonAt(row, "Repair", () => Commands?.Configure(g, DeployAction.Repair));
            repairButtons[i].CustomMinimumSize = new(130, 0);
            replaceButtons[i] = ButtonAt(row, "Replace", () => Commands?.Configure(g, DeployAction.Replace));
            replaceButtons[i].CustomMinimumSize = new(170, 0);
        }

        modalNotice = LabelAt(column, "", 14, Palette.Ui.Accent);
        modalNotice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalNotice.CustomMinimumSize = new(780, 0);
        modalAction = ButtonAt(column, "", ModalAction);

        var storage = new HBoxContainer();
        column.AddChild(storage);
        slotButton = ButtonAt(storage, "Slot 1", () => Commands?.CycleSaveSlot(), CommandIds.Slot);
        ButtonAt(storage, "Save campaign", () => Commands?.Save(), CommandIds.Save);
        ButtonAt(storage, "Load checkpoint", () => Commands?.Load(), CommandIds.Load);
        ButtonAt(storage, "Resume autosave", () => Commands?.LoadAutosave(), CommandIds.Autosave);
        ButtonAt(storage, "New campaign", () => Commands?.NewCampaign());
        modeButton = ButtonAt(storage, ModeButtonText(GameMode.Modern), () => Commands?.SwitchMode());
        modeButton.TooltipText = "Classic rules: eight vehicle types with real armour values, sabot, HEAT, missiles and machine guns, " +
            "jungle that hides and protects, roads that are fast but exposed, and defenders that hold their ground. Starts a new campaign.";
    }

    /// <summary>The caption of the button that switches away from <paramref name="current"/>.</summary>
    public static string ModeButtonText(GameMode current) => current == GameMode.Classic ? "Play Modern rules" : "Play Classic rules";

    private void ModalAction()
    {
        switch (sim?.Phase)
        {
            case MissionPhase.Briefing:
                Commands?.Deploy();
                break;
            case MissionPhase.Victory when !Mission.FinalBattle(sim.Campaign):
                Commands?.ContinueCampaign();
                break;
            case MissionPhase.Victory:
                Commands?.NewCampaign();
                break;
            default:
                Commands?.Restart();
                break;
        }
    }

    private void RefreshModal(Simulation s)
    {
        lastPhase = s.Phase;
        modal.Visible = s.Phase != MissionPhase.Active;
        modalNotice.Text = "";
        bool final = Mission.FinalBattle(s.Campaign);
        bool islandDone = s.Campaign.Battle == Mission.BattleCount(s.Campaign) - 1;
        modeButton.Text = ModeButtonText(s.Campaign.Mode);
        zoneMap.Show(s.Campaign);
        modalBody.CustomMinimumSize = new(s.Classic ? 610 : 780, 0);
        campaignStrip.Text = $"{s.Campaign.Mode.ToString().ToUpperInvariant()}    " + string.Join("  →  ", Enumerable.Range(0, Mission.Count).Select(i =>
        {
            string mark = i < s.Campaign.Island ? "✓" : i == s.Campaign.Island ? "▶" : "○";
            string battles = s.Classic && i == s.Campaign.Island ? $" ({s.Campaign.Battle + 1}/{Mission.BattleCount(s.Campaign)})" : "";
            return $"{mark} {i + 1:00} {Mission.Names[i].Replace("OPERATION ", "")}{battles}";
        }));
        modalTitle.Text = s.Phase switch
        {
            MissionPhase.Briefing => s.ZoneBattle?.Name ?? Mission.Names[s.Campaign.Island],
            MissionPhase.Victory => final ? "CAMPAIGN COMPLETE" : islandDone ? "ISLAND SECURED" : "ZONE SECURED",
            _ => s.BrokenCondition() != null ? "MISSION FAILED" : "TASK FORCE LOST",
        };
        modalBody.Text = s.Phase == MissionPhase.Briefing ? BriefingText(s) : OutcomeText(s, final);
        modalAction.Text = s.Phase switch
        {
            MissionPhase.Briefing => $"Deploy task force  [{Settings.Hint(CommandIds.Deploy)}]",
            MissionPhase.Victory => final ? "Start another campaign" : islandDone ? "Continue to next island" : "Continue to next battle",
            _ => $"Retry this island  [{Settings.Hint(CommandIds.Restart)}]",
        };
        deployment.Visible = s.Phase == MissionPhase.Briefing;
        modalAction.Disabled = s.Phase == MissionPhase.Briefing && !s.Vehicles.Any(v => v.Alive && v.Player);
        budget.Text = s.Classic
            ? $"FUNDS  /  {Campaign.Money(s.Campaign.Supplies, GameMode.Classic)} available     ·     All guns rearmed before deployment"
            : $"LOGISTICS  /  {s.Campaign.Supplies} supplies available     ·     All guns rearmed before deployment";
        for (int i = 0; i < Mission.GroupCount; i++)
        {
            refitButtons[i].TooltipText = RefitTooltip(s.Campaign.Mode);
            RefreshDeploymentRow(s.Campaign, i);
        }
    }

    private void RefreshDeploymentRow(Campaign campaign, int group)
    {
        var roster = campaign.Roster.Where(r => Mission.GroupOf(r.Id) == group).ToArray();
        int count = roster.Count(r => r.Health > 0);
        rosterLabels[group].Text = $"{Mission.GroupNames[group]}  {count}/{Mission.VehiclesPerGroup}\nHull strength {roster.Sum(r => r.Health) / Mission.VehiclesPerGroup:0}%";

        int refit = campaign.RefitCost(group);
        int repair = campaign.RepairCost(group);
        int replace = campaign.ReplacementCost(group);
        string refitPrice = refit <= 0 ? $"+{Amount(campaign, -refit)}" : $"−{Amount(campaign, refit)}";
        refitButtons[group].Text = $"{campaign.LoadoutOf(group)} → {campaign.NextLoadout(group)} ({refitPrice})";
        refitButtons[group].Disabled = refit > campaign.Supplies;
        repairButtons[group].Text = repair == 0 ? "Repaired" : $"Repair (−{Amount(campaign, repair)})";
        repairButtons[group].Disabled = repair == 0 || repair > campaign.Supplies;
        replaceButtons[group].Text = replace == 0 ? "Fully staffed" : $"Replace one (−{Amount(campaign, replace)})";
        replaceButtons[group].Disabled = replace == 0 || replace > campaign.Supplies;
    }

    /// <summary>A price on a button: a bare number of supplies in Modern mode, dollars in Classic.</summary>
    private static string Amount(Campaign campaign, int amount) =>
        campaign.Mode == GameMode.Classic ? Campaign.Money(amount, GameMode.Classic) : amount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string RefitTooltip(GameMode mode) => mode == GameMode.Classic
        ? "Armor: 4 M1 Abrams. Mixed: 2 Abrams + 2 ITV missile carriers, which outrange tanks but fire only at a halt. " +
            "Recon: 2 M2 Bradleys + 2 M113s, fast and light. Refitting preserves existing damage."
        : "Armor: 4 main tanks. Mixed: 2 main tanks + 2 scouts. Recon: 4 scouts. Scouts move and aim faster but have lighter armor and shorter gun range. Refitting preserves existing damage.";

    private string BriefingText(Simulation s) => s.Classic ? ClassicBriefingText(s) :
        Mission.Briefings[s.Campaign.Island] +
        $"\n\nHold each station for {CaptureSite.HoldSeconds:0} seconds without defenders nearby. Right-click to move; Shift queues routes; " +
        $"{Settings.Hint(CommandIds.Pause)} pauses to plan. Scouts can observe farther. Enemy contacts disappear behind cover; " +
        $"hollow markers show their last known positions for {Reconnaissance.MemorySeconds:0} seconds." +
        "\n\nLosses and damage carry forward. Return to the southern beach for field resupply; all guns rearm during transit.";

    private string ClassicBriefingText(Simulation s) =>
        (s.ZoneBattle?.Briefing ?? Mission.Briefings[s.Campaign.Island]) +
        "\n\nCLASSIC RULES. Roads are fast; jungle is slow, but its edge is the best cover on the island and spoils enemy aim. " +
        $"Deep jungle hides you until you fire, which gives you away for {ClassicRules.RevealSeconds:0} seconds. Rivers can be forded anywhere, slowly. " +
        "Guns pick sabot for tanks and HEAT for carriers, and changing ammunition means reloading. Missiles reach farthest but fire only at a halt. " +
        (s.Night ? $"NIGHT BATTLE: your thermal sights see as far as by day, the enemy only {ClassicRules.NightEnemySight:0%} as far, until your muzzle flash gives you away. " : "") +
        ConditionsText(s.Conditions) +
        $"Each enemy vehicle destroyed pays its price as a bounty; knocking down a civilian building costs {Campaign.Money(Campaign.CivilianFine, GameMode.Classic)}. " +
        (s.ZoneBattle?.Objective is ObjectiveKind.Capture or null ? $"Defenders hold their posts: destroy each garrison, then hold its station for {CaptureSite.HoldSeconds:0} seconds. " : "Defenders hold their posts. ") +
        $"{Settings.Hint(CommandIds.Pause)} pauses to plan.";

    /// <summary>How far the battle's objective got, for the outcome screen.</summary>
    private static string ObjectiveSummary(Simulation s) => s.ZoneBattle?.Objective switch
    {
        ObjectiveKind.Destroy => s.TargetHealth <= 0 ? "Target destroyed" : $"Target standing at {s.TargetHealth:0}%",
        ObjectiveKind.Clear => $"Enemy remaining  {s.Vehicles.Count(v => v.Enemy && v.Alive)}",
        ObjectiveKind.Escort => $"Supply trucks delivered  {s.Delivered}/{s.ZoneBattle.Convoy!.Trucks}",
        ObjectiveKind.Ambush => $"Convoy destroyed  {s.ColumnKills}/{s.ZoneBattle.ColumnVehicles}    ·    Escaped  {s.Escaped}",
        ObjectiveKind.Hold => $"Site {(s.Sites.All(site => site.Secured) ? "held" : "not held")}    ·    Time  {TimeSpan.FromSeconds(s.Elapsed):mm\\:ss} of {TimeSpan.FromSeconds(s.ZoneBattle.HoldSeconds):mm\\:ss}",
        _ => $"Stations secured  {s.Sites.Count(site => site.Secured)}/{s.Sites.Count}",
    };

    /// <summary>The battle's extra terms, for the briefing; empty when there are none.</summary>
    private static string ConditionsText(BattleConditions conditions)
    {
        List<string> terms = [];
        if (conditions.MaxLosses is int most)
        {
            terms.Add($"lose no more than {most} vehicles");
        }

        if (conditions.TimeLimitSeconds is float limit)
        {
            terms.Add($"secure every station within {limit / 60:0} minutes");
        }

        if (conditions.SpareCivilians)
        {
            terms.Add("destroy no civilian building");
        }

        return terms.Count == 0 ? "" : $"THIS MISSION: {string.Join(", and ", terms)}, or it fails. ";
    }

    private static string OutcomeText(Simulation s, bool final)
    {
        int survivors = s.Vehicles.Count(v => v.Alive && v.Player);
        int destroyed = s.Vehicles.Count(v => !v.Alive && v.Enemy);
        int losses = s.Campaign.PreviousLosses + s.Vehicles.Count(v => !v.Alive && v.Player);
        string pay = s.Classic
            ? $"{Campaign.Money(Campaign.ClassicVictoryPay, GameMode.Classic)} pay plus {Campaign.Money(s.Bounty, GameMode.Classic)} in bounties, less {Campaign.Money(s.Fines, GameMode.Classic)} in fines,"
            : $"{Campaign.VictorySupplies} supplies";
        string failure = s.Phase == MissionPhase.Defeat && s.BrokenCondition() is string reason ? $"{reason}. " : "";
        string next = s.Phase != MissionPhase.Victory
            ? failure + "Retry restores your force and supplies to this island's deployment state. Earlier campaign losses remain."
            : final
                ? "All three islands are secure. Your campaign is complete."
                : s.Classic
                    ? $"Objective achieved. Continue to receive {pay} and prepare your surviving force for the next {(s.Campaign.Battle == Mission.BattleCount(s.Campaign) - 1 ? "island" : "battle")}."
                    : $"The radio network is secure. Continue to receive {pay} and prepare your surviving force for the next island.";
        return
            $"Mission time  {TimeSpan.FromSeconds(s.Elapsed):mm\\:ss}    ·    Campaign time  {TimeSpan.FromSeconds(s.Campaign.PreviousTime + s.Elapsed):mm\\:ss}\n" +
            $"Friendly survivors  {survivors}/{Mission.FriendlySlots}    ·    Defenders destroyed  {destroyed}/{s.Vehicles.Count(v => v.Enemy)}\n" +
            $"{ObjectiveSummary(s)}    ·    Campaign losses  {losses}\n\n" +
            next;
    }
}
