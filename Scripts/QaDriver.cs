namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>
/// Opt-in runtime checks and previews, driven from the command line (see README, "Build and
/// verify"). Only added to the scene when a QA switch is present, so normal play never runs it.
/// Checks push real input events through the viewport, so they exercise Godot's input dispatch
/// and the actual controls, and print a PASS marker that <c>Tools/Check.ps1</c> looks for.
/// </summary>
/// <remarks>
/// Godot instantiates nodes through a parameterless constructor, so collaborators arrive via
/// <see cref="Start"/> rather than a primary constructor.
/// </remarks>
public partial class QaDriver : Node
{
    // The smoke test's timeline, in physics frames (60 per second).
    private const int SmokeChaseFrame = 240;
    private const int SmokePauseFrame = 360;
    private const int SmokeResumeFrame = 390;
    private const int SmokeCheckFrame = 600;

    /// <summary>The staged skirmish holds fire until this physics frame, so screenshots can be timed after it.</summary>
    private const int SkirmishOpenFireFrame = 60;

    // Assigned in Start, immediately after the node is added.
    private Battle battle = null!;
    private LaunchOptions options = null!;

    private int frame;
    private bool takingScreenshot;
    private bool mouseEntered;
    private int renderedFrames;
    private readonly List<double> frameTimes = [];

    private Simulation Sim => battle.Sim;

    public void Start(Battle owner, LaunchOptions launch)
    {
        battle = owner;
        options = launch;
        if (options.SceneryTest)
        {
            Run("SCENERY", SceneryCheck);
            return;
        }
        if (options.SmokeTest || options.Preview)
        {
            battle.Deploy();
            Sim.Order(0, new(-53, 10));
            Sim.Order(1, new(-8, 24));
            Sim.Order(2, new(17, 30));
            Sim.Order(3, new(45, 34));
        }

        if (options.BattlePreview)
        {
            battle.Deploy();
            StageSkirmish();
        }

        if (options.CapturePreview)
        {
            battle.Deploy();
            StageCaptures();
        }

        if (options.CounterAttackPreview)
        {
            battle.Deploy();
            StageCounterAttack();
        }

        if (options.ChasePreview)
        {
            battle.Deploy();
            battle.ShowView(CameraView.Chase);
            battle.Selected = 2;
        }

        if (options.LineupPreview)
        {
            battle.Deploy();
            StageLineup();
        }

        if (options.SettingsPreview)
        {
            battle.OpenSettings();
        }

        if (options.SupportPreview)
        {
            battle.Deploy();
            StageSupport();
        }

        if (options.SightPreview || options.WindowsPreview)
        {
            battle.Deploy();
            battle.Selected = 2;
            battle.ShowView(options.WindowsPreview ? CameraView.Windows : CameraView.Sight);
            if (options.WindowsPreview)
            {
                // Show all three window modes: sights for Alpha and Charlie, Bravo's map and Delta's status.
                var windows = battle.Hud.FindChildren("*", nameof(PanelContainer), true, false).OfType<GroupWindow>().ToList();
                windows[1].SetMode(WindowMode.Map);
                windows[3].SetMode(WindowMode.Status);
            }

            if (Sim.Leader(2) is Vehicle gunner)
            {
                // Laid on the staged defenders, due north.
                gunner.TurretHeading = 0;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (options.BattlePreview && ++frame == SkirmishOpenFireFrame)
        {
            foreach (var v in Sim.Vehicles)
            {
                v.HoldFire = false;
            }
        }

        if (options.SmokeTest)
        {
            frame++;
            SmokeTest();
        }
        else if (options.ViewStateTest)
        {
            frame++;
            Run("VIEW STATE", ViewStateCheck);
        }
        else if (options.ClassicTest)
        {
            frame++;
            Run("CLASSIC", ClassicCheck);
        }
        else if (options.CampaignTest)
        {
            frame++;
            Run("UI", CampaignCheck);
        }
        else if (options.UiTest)
        {
            frame++;
            Run("UI", UiCheck);
        }
        else if (options.SettingsReloadTest)
        {
            frame++;
            Run("SETTINGS", SettingsReloadCheck);
        }
        else if (options.SettingsTest)
        {
            frame++;
            Run("SETTINGS", SettingsCheck);
        }
    }

    public override void _Process(double delta)
    {
        if (options.ScreenshotPath != null && ++renderedFrames > 90)
        {
            frameTimes.Add(delta * 1000);
        }
        if (options.ScreenshotPath != null && !takingScreenshot && Engine.GetFramesDrawn() > options.ScreenshotFrame)
        {
            takingScreenshot = true;
            // Capture once the frame has finished drawing.
            RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, Callable.From(SaveScreenshot), (uint)ConnectFlags.OneShot);
        }
    }

    private static void Check(bool ok, string name)
    {
        if (!ok)
        {
            throw new InvalidOperationException(name);
        }
    }

    private void Run(string suite, Action check)
    {
        try
        {
            check();
        }
        catch (Exception ex)
        {
            // Any failure, including an unexpected exception, must fail the run rather than hang it.
            GD.PushError($"{suite} FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    private void Pass(string message)
    {
        GD.Print(message);
        GetTree().Quit();
    }

    private void SaveScreenshot()
    {
        var error = GetViewport().GetTexture().GetImage().SavePng(options.ScreenshotPath);
        GD.Print($"Screenshot: {error} {options.ScreenshotPath}");
        var ordered = frameTimes.Order().ToArray();
        if (ordered.Length > 0)
        {
            GD.Print($"GAME FRAME TIMES: {ordered.Length} samples, median {ordered[ordered.Length / 2]:0.00} ms, p95 {ordered[(int)((ordered.Length - 1) * .95)]:0.00} ms; process intervals including VSync, not GPU timings.");
        }
        GD.Print($"GAME DRAW: {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} calls, {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)} primitives; {GetViewport().GetTexture().GetWidth()} x {GetViewport().GetTexture().GetHeight()}.");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private void KeyPress(Key key)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }

    private void Click(Control control, MouseButton button = MouseButton.Left)
    {
        var p = control.GetGlobalRect().GetCenter();
        if (!mouseEntered)
        {
            // Once only: Godot warns if told again.
            GetViewport().NotifyMouseEntered();
            mouseEntered = true;
        }

        GetViewport().PushInput(new InputEventMouseMotion { GlobalPosition = p, Position = p }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, GlobalPosition = p, Position = p, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, GlobalPosition = p, Position = p, Pressed = false }, true);
    }

    private Button HudButton(string text) =>
        battle.Hud.FindChildren("*", nameof(Button), true, false).OfType<Button>().First(b => b.Text.Contains(text));

    private void SmokeTest()
    {
        switch (frame)
        {
            case SmokeChaseFrame:
                battle.Selected = 2;
                battle.CameraRig.Tactical = false;
                break;
            case SmokePauseFrame:
                battle.CameraRig.Tactical = true;
                Sim.Paused = true;
                Sim.Order(2, new(40, -35), true);
                break;
            case SmokeResumeFrame:
                Sim.Paused = false;
                // Bring the first reinforcement wave forward, so the view must build vehicles mid-battle.
                Sim.Elapsed = Math.Max(Sim.Elapsed, Simulation.FirstWaveSeconds - 1);
                break;
            case SmokeCheckFrame:
                bool valid = Sim.Vehicles.All(v => Geometry.Finite(v.Position) && Sim.Navigation.Clear(v.Position, v.Position, Mission.VehicleRadius));
                bool overlap = Sim.Vehicles.Any(a => Sim.Vehicles.Any(b => a.Id < b.Id && V2.Distance(a.Position, b.Position) < Mission.VehicleRadius * 2 - .01f));
                int landed = Sim.Vehicles.Count(v => v.Id >= Mission.FirstReinforcementId);
                if (!valid || overlap)
                {
                    GD.PushError("SMOKE FAIL: invalid vehicle position or overlap");
                    GetTree().Quit(1);
                }
                else if (Sim.WavesLanded == 0 || landed == 0)
                {
                    GD.PushError("SMOKE FAIL: the reinforcement wave did not land");
                    GetTree().Quit(1);
                }
                else
                {
                    Pass($"SMOKE PASS: {Sim.Vehicles.Count} vehicles ({landed} landed as reinforcements), {Sim.ShotsFired} shots, {Sim.Elapsed:0.0}s simulated; cameras and paused orders exercised.");
                }
                break;
        }
    }

    /// <summary>
    /// Brings three defenders broadside-on to Charlie, one nearly dead and one smoking, and zooms
    /// the tactical camera in, so a screenshot catches flank hits, a kill and a burning wreck.
    /// </summary>
    /// <summary>Every vehicle kind in two rows on open ground south of the island's centre, turrets turned to show the cut.</summary>
    private void StageLineup()
    {
        const int perRow = 6;
        const float spacing = 7, rowGap = 10, rowsAt = 40, turretTurn = 60;
        var kinds = Enum.GetValues<VehicleKind>();
        Vector3? focus = null;
        for (int i = 0; i < kinds.Length; i++)
        {
            var body = battle.Scene.Body(kinds[i], false, false);
            battle.Scene.AddChild(body.Hull);
            body.Hull.Position = new((i % perRow - (perRow - 1) / 2f) * spacing, 0, rowsAt + i / perRow * rowGap);
            body.Turret.RotationDegrees = new(0, turretTurn, 0);
            if (options.LineupFocus == kinds[i])
            {
                focus = body.Hull.Position;
            }
        }

        foreach (var v in Sim.Vehicles)
        {
            v.HoldFire = true;
        }

        battle.Hud.Visible = false;
        // The whole line-up from above and behind, or one vehicle from its front quarter.
        var camera = new Camera3D { Current = true, Fov = 50, Position = focus is { } at ? at + new Vector3(-5, 3.5f, -6) : new(0, 11, rowsAt + rowGap + 16) };
        battle.AddChild(camera);
        camera.LookAt(focus is { } target ? target + Vector3.Up : new(0, 0, rowsAt + rowGap / 2));
    }

    private void StageSkirmish()
    {
        const float range = 34, spacing = 9, nearlyDead = 5, smoking = 38, zoomSteps = 9;
        if (Sim.Leader(2) is not Vehicle leader)
        {
            return;
        }

        var defenders = Sim.Vehicles.Where(v => v.Enemy && v.Alive).Take(3).ToList();
        for (int i = 0; i < defenders.Count; i++)
        {
            var defender = defenders[i];
            defender.Position = defender.Home = leader.Position + new V2((i - 1) * spacing, -range);
            defender.Heading = MathF.PI / 2;
            defender.TurretHeading = MathF.PI;
            defender.Route.Clear();
            defender.Health = i == 1 ? nearlyDead : smoking;
        }

        foreach (var v in Sim.Vehicles)
        {
            v.HoldFire = true;
        }

        battle.Selected = 2;
        battle.CameraRig.FocusOn(leader.Position + new V2(0, -range / 2));
        for (int i = 0; i < zoomSteps; i++)
        {
            battle.CameraRig.ZoomIn();
        }
    }

    /// <summary>
    /// Delta holds the first objective under fire (contested), Bravo is halfway through the second,
    /// and Charlie is a moment from securing the third, so a screenshot shows every capture state.
    /// </summary>
    private void StageCaptures()
    {
        const float contested = .7f, halfway = .45f, nearlySecured = .97f;
        float[] progress = [contested, halfway, nearlySecured];
        int[] groups = [3, 1, 2];
        for (int i = 0; i < Sim.Sites.Count; i++)
        {
            Sim.Sites[i].Progress = CaptureSite.HoldSeconds * progress[i];
            PlaceGroup(groups[i], Sim.Sites[i].Position);
        }

        // Only the first objective stays defended; the others' garrisons are already wrecks.
        foreach (var defender in Sim.Vehicles.Where(v => v.Enemy && Sim.Sites.Skip(1).Any(s => V2.Distance(v.Position, s.Position) < CaptureSite.EnemyRadius * 2)))
        {
            defender.Health = 0;
        }

        battle.CameraRig.FocusOn((Sim.Sites[0].Position + Sim.Sites[1].Position + Sim.Sites[2].Position) / Sim.Sites.Count);
    }

    /// <summary>
    /// Delta has just taken the first objective and held it long enough to provoke a counter-attack,
    /// and the first reinforcement wave is moments from landing, so a screenshot a couple of seconds
    /// in shows both alerts, the threatened site and the minimap pings.
    /// </summary>
    private void StageCounterAttack()
    {
        const float landingLead = 1.5f;
        var site = Sim.Sites[0];
        site.Progress = CaptureSite.HoldSeconds;
        site.SecuredFor = Simulation.CounterAttackDelay;
        foreach (var defender in Sim.Vehicles.Where(v => v.Enemy && V2.Distance(v.Home, site.Position) < CaptureSite.EnemyRadius))
        {
            defender.Health = 0;
        }

        PlaceGroup(3, site.Position);
        Sim.Elapsed = Simulation.FirstWaveSeconds - landingLead;
        battle.CameraRig.FocusOn((site.Position + Mission.LandingPoint(Sim.Campaign.Island)) / 2);
    }

    /// <summary>
    /// Charlie advances on the first objective behind smoke, having laid a mine, with its own fire
    /// mission about to land on the garrison and an enemy one on its way, so a screenshot shows them all.
    /// </summary>
    private void StageSupport()
    {
        const float approach = 30, zoomSteps = 7, firstRoundLead = .5f;
        var site = Sim.Sites[0];
        PlaceGroup(2, site.Position + new V2(0, approach));
        battle.Selected = 2;
        foreach (var v in Sim.Group(2))
        {
            v.Heading = v.TurretHeading = 0;
        }

        Sim.LayMine(2);
        Sim.FireSmoke(2);
        Sim.CallArtillery(site.Position, ArtilleryShell.HighExplosive);
        Sim.EnemyArtilleryLeft = 1;
        Sim.CallArtillery(site.Position + new V2(-12, approach + 8), ArtilleryShell.HighExplosive, enemy: true);
        Sim.Strikes[0].NextRound = Sim.Elapsed + firstRoundLead;
        battle.CameraRig.FocusOn(site.Position + new V2(0, approach / 2));
        for (int i = 0; i < zoomSteps; i++)
        {
            battle.CameraRig.ZoomIn();
        }
    }

    /// <summary>Parks a group's vehicles on clear ground around <paramref name="center"/>.</summary>
    private void PlaceGroup(int group, V2 center)
    {
        const float step = 3, turn = 2.4f;
        var placed = new List<V2>();
        float distance = 0, angle = 0;
        foreach (var vehicle in Sim.Group(group))
        {
            V2 p;
            do
            {
                distance += step / 4;
                angle += turn;
                p = center + distance * new V2(MathF.Cos(angle), MathF.Sin(angle));
            }
            while (!Sim.Navigation.Clear(p, p, Mission.VehicleRadius) ||
                Sim.Vehicles.Any(v => v != vehicle && V2.Distance(v.Position, p) < Mission.VehicleRadius * 2 + step) ||
                placed.Any(q => V2.Distance(q, p) < Mission.VehicleRadius * 2 + step));

            vehicle.Position = p;
            vehicle.Orders.Clear();
            vehicle.Route.Clear();
            placed.Add(p);
        }
    }

    private void SecureAllSites()
    {
        foreach (var site in Sim.Sites)
        {
            site.Progress = CaptureSite.HoldSeconds;
        }

        Sim.Phase = MissionPhase.Victory;
    }

    private void CampaignCheck()
    {
        switch (frame)
        {
            case 10:
                Click(HudButton("Armor → Mixed"));
                break;
            case 12:
                Check(Sim.Campaign.LoadoutOf(0) == Loadout.Mixed && Sim.Campaign.Supplies == 130, "Deployment refit");
                Click(HudButton("Deploy task force"));
                break;
            case 14:
                Check(Sim.Phase == MissionPhase.Active, "Campaign deploy");
                Sim.Vehicles[0].Health = 0;
                Sim.Vehicles[1].Health = 50;
                SecureAllSites();
                break;
            case 16:
                KeyPress(Key.R);
                Check(Sim.Phase == MissionPhase.Victory, "Restart key keeps a won island");
                Click(HudButton("Continue to next island"));
                break;
            case 18:
                Check(Sim.Campaign.Island == 1 && Sim.Campaign.Supplies == 230 && Sim.Vehicles.All(v => v.Id != 0), "Continue carries roster");
                Click(HudButton("Repair ("));
                break;
            case 20:
                Check(Sim.Campaign.Supplies == 222 && Sim.Campaign.Roster[1].Health == 100, "Depot repair");
                Click(HudButton("Replace one"));
                break;
            case 22:
                Check(Sim.Campaign.Roster[0].Health == 100 && Sim.Campaign.Supplies == 182, "Depot replacement");
                Click(HudButton("Save campaign"));
                break;
            case 24:
                Click(HudButton("Armor → Mixed"));
                break;
            case 26:
                Click(HudButton("Load checkpoint"));
                break;
            case 28:
                Check(Sim.Campaign.Island == 1 && Sim.Campaign.Supplies == 182 && Sim.Phase == MissionPhase.Briefing, "Restore deployment");
                Click(HudButton("Deploy task force"));
                break;
            case 30:
                foreach (var v in Sim.Vehicles.Where(v => !v.Enemy))
                {
                    v.Health = 0;
                }

                Sim.Phase = MissionPhase.Defeat;
                break;
            case 32:
                Click(HudButton("Retry this island"));
                break;
            case 34:
                Check(Sim.Campaign.Island == 1 && Sim.Campaign.Supplies == 182 && Sim.Vehicles.Count(v => v.Alive && !v.Enemy) == Mission.FriendlySlots, "Island retry");
                SecureAllSites();
                break;
            case 36:
                Click(HudButton("Continue to next island"));
                break;
            case 38:
                Check(Sim.Campaign.Island == 2 && Sim.Campaign.Supplies == 282, "Final island transition");
                SecureAllSites();
                break;
            case 40:
                Check(battle.Hud.FindChildren("*", nameof(Label), true, false).OfType<Label>().Any(l => l.Text == "CAMPAIGN COMPLETE"), "Campaign outcome screen");
                Click(HudButton("Start another campaign"));
                break;
            case 42:
                Check(Sim.Campaign.Island == 0 && Sim.Campaign.Supplies == Campaign.StartingSupplies && Sim.Phase == MissionPhase.Briefing, "New campaign reset");
                Pass("CAMPAIGN PASS: deployment refit, island transitions, repair/replacement, save/load, retry, completion and new campaign through input dispatch.");
                break;
        }
    }

    private void UiCheck()
    {
        switch (frame)
        {
            case 10:
                Click(HudButton("Deploy task force"));
                break;
            case 12:
                Check(Sim.Phase == MissionPhase.Active, "Deploy button");
                Click(HudButton("BRAVO"));
                break;
            case 14:
                Check(battle.Selected == 1, "Group button");
                Click(HudButton("Pause"));
                break;
            case 16:
                Check(Sim.Paused, "Pause button");
                Click(battle.Hud.FindChildren("*", nameof(Control), true, false).OfType<TacticalMap>().Single(m => m.IsVisibleInTree()), MouseButton.Right);
                break;
            case 18:
                Check(Sim.Group(1).All(v => v.Orders.Count > 0), "Minimap orders while paused");
                Click(HudButton("Stop movement"));
                break;
            case 20:
                Check(Sim.Group(1).All(v => v.Orders.Count == 0), "Stop button");
                Click(HudButton("Hold fire"));
                break;
            case 22:
                Check(Sim.Group(1).All(v => v.HoldFire), "Fire discipline button");
                KeyPress(Key.F6);
                break;
            case 24:
                Check(File.Exists(battle.SavePath), "Save key");
                Sim.Vehicles[0].Health = 25;
                KeyPress(Key.F9);
                break;
            case 26:
                Check(Sim.Paused && Sim.Vehicles[0].Health == 100, "Load key and scene rebuild");
                KeyPress(Key.Tab);
                break;
            case 28:
                Check(!battle.CameraRig.Tactical, "Camera key");
                KeyPress(Key.Key3);
                break;
            case 30:
                Check(battle.Selected == 2, "Group key");
                Click(HudButton("Regroup"));
                break;
            case 32:
                Check(Sim.Group(2).Count(v => v.Orders.Count > 0) == 3, "Regroup button");
                KeyPress(Key.P);
                break;
            case 34:
                Check(!Sim.Paused, "Resume key");
                KeyPress(Key.P);
                Sim.Vehicles[0].Health = 55;
                KeyPress(Key.F7);
                KeyPress(Key.F6);
                break;
            case 36:
                Check(battle.SaveSlot == 1 && File.Exists(battle.SavePath), "Second save slot");
                KeyPress(Key.F7);
                KeyPress(Key.F7);
                KeyPress(Key.F9);
                break;
            case 38:
                Check(battle.SaveSlot == 0 && Sim.Vehicles[0].Health == 100, "Manual slots remain independent");
                KeyPress(Key.F10);
                break;
            case 40:
                Check(Sim.Phase == MissionPhase.Briefing && Sim.Elapsed == 0, "Deployment autosave restore");
                Pass("UI PASS: deployment, command controls, three save slots, independent autosave restoration and camera input dispatch.");
                break;
        }
    }
}
