namespace Breakwater;

/// <summary>Regression coverage for thermal/callout state across views, new games, saves and islands.</summary>
public partial class QaDriver
{
    private const string OldBattleNotice = "Previous battle kill report";
    private GunSight FullSight => battle.Hud.FindChildren("*", nameof(Control), true, false).OfType<GunSight>().Single();

    private void ViewStateCheck()
    {
        switch (frame)
        {
            case 10:
                StartNightFixture();
                Sim.Elapsed = 120;
                break;
            case 12:
                Check(battle.CameraRig.Camera.Environment != null, "Night gun sight is thermal");
                ReportFixtureKill();
                battle.Hud.Notify(OldBattleNotice);
                KeyPress(Key.Tab);
                break;
            case 14:
                Check(battle.View == CameraView.Windows, "Sight switches to group windows");
                Check(battle.CameraRig.Camera.Environment == null, "Thermal override removed from covered main camera");
                CheckGroupSightEnvironments(night: true);
                KeyPress(Key.Tab);
                break;
            case 13:
                CaptureViewState("night-group-callout");
                break;
            case 15:
                CaptureViewState("night-tactical");
                break;
            case 16:
                Check(battle.View == CameraView.Tactical && battle.CameraRig.Camera.Environment == null, "Night tactical view uses the normal moonlit environment");
                Check(GroupWindows().All(w => WindowViewport(w).RenderTargetUpdateMode == SubViewport.UpdateMode.Disabled), "Hidden group cameras stop rendering");
                battle.Maximise(0, WindowMode.Sight);
                break;
            case 18:
                Check(battle.CameraRig.Camera.Environment != null, "Returning to night sight restores thermal");
                battle.CameraRig.Update(0, null);
                Check(battle.CameraRig.Camera.Environment == null, "No-leader fallback clears thermal");
                battle.CameraRig.Update(0, Sim.Leader(0));
                battle.NewCampaign();
                battle.NewCampaign();
                CheckFreshViewState();
                Check(!battle.CameraRig.Night && !Sim.Night, "New daytime campaign updates night state immediately");
                DeployPaused();
                break;
            case 20:
                CheckGroupSightEnvironments(night: false);
                Check(battle.Hud.FindChildren("*", nameof(Label), true, false).OfType<Label>().All(l => l.Text != OldBattleNotice), "Old battle notice is cleared");
                battle.Maximise(0, WindowMode.Sight);
                break;
            case 19:
                CaptureViewState("new-day-group");
                break;
            case 21:
                CaptureViewState("new-day-sight");
                break;
            case 22:
                Check(battle.CameraRig.Camera.Environment == null, "New daytime sight has no thermal override");
                ReportFixtureKill();
                Sim.Elapsed = 3;
                Check(FullSight.ShotCall == null && GroupWindows().All(w => w.ShotCall == null), "Shot calls still expire during the current battle");
                Sim.Elapsed = 0;
                ReportFixtureKill();
                battle.Save();
                Check(File.Exists(battle.SavePath), "Fixture checkpoint saved");
                var savedSimulation = Sim;
                battle.Load();
                Check(Sim != savedSimulation && Sim.Elapsed == 0, "Checkpoint restores at the same timestamp");
                CheckFreshViewState();
                battle.Maximise(0, WindowMode.Sight);
                break;
            case 24:
                ReportFixtureKill();
                battle.Restart();
                battle.Restart();
                Check(Sim.Phase == MissionPhase.Briefing, "Retry rebuilds the battle");
                CheckFreshViewState();
                DeployPaused();
                battle.Maximise(0, WindowMode.Sight);
                break;
            case 26:
                ReportFixtureKill();
                battle.SelectGroup(1);
                break;
            case 28:
                Check(FullSight.ShotCall == null, "Changing gunner clears the previous gun's callout");
                Check(GroupWindows()[0].ShotCall != null, "A group's own recent result survives selecting another group");
                Sim.Phase = MissionPhase.Victory;
                battle.ContinueCampaign();
                Check(Sim.Campaign.Battle == 1, "Continue advances the Classic battle");
                CheckFreshViewState();
                StartNightFixture(lastBattle: true);
                break;
            case 30:
                Check(battle.CameraRig.Camera.Environment != null, "Night state returns for a later night mission");
                ReportFixtureKill();
                Sim.Phase = MissionPhase.Victory;
                battle.ContinueCampaign();
                Check(Sim.Campaign.Island == 2 && !Sim.Night && !battle.CameraRig.Night, "Island transition immediately restores daytime state");
                CheckFreshViewState();
                DeployPaused();
                battle.Maximise(0, WindowMode.Sight);
                break;
            case 32:
                Check(battle.CameraRig.Camera.Environment == null, "Next island starts with a normal daytime sight");
                battle.SwitchMode();
                battle.SwitchMode();
                Check(!Sim.Classic && !Sim.Night, "Switch to Modern starts a day battle");
                CheckFreshViewState();
                DeployPaused();
                KeyPress(Key.Tab);
                break;
            case 34:
                Check(battle.View == CameraView.Chase && battle.CameraRig.Camera.Environment == null, "Modern chase view remains non-thermal");
                KeyPress(Key.Tab);
                break;
            case 36:
                Check(battle.View == CameraView.Tactical && battle.CameraRig.Camera.Environment == null, "Modern tactical view remains non-thermal");
                Pass("VIEW STATE PASS: thermal scopes, tactical/chase/group views, lost leaders, new campaigns, same-time save/load, retries, next battles/islands and shot-call expiry/reset.");
                break;
        }
    }

    private void StartNightFixture(bool lastBattle = false)
    {
        var campaign = Campaign.Start(GameMode.Classic);
        campaign.Island = Mission.ClassicNightIsland;
        campaign.Battle = lastBattle ? ClassicCampaign.FinalBattle(campaign.Island) : 0;
        battle.ReplaceSimulation(new Simulation(campaign));
        DeployPaused();
        battle.Maximise(0, WindowMode.Sight);
    }

    private void DeployPaused()
    {
        battle.Deploy();
        Sim.Paused = true;
    }

    private void ReportFixtureKill()
    {
        var gunner = Sim.Leader(0)!;
        var enemy = Sim.Vehicles.FirstOrDefault(v => v.Enemy);
        bool temporaryTarget = enemy == null;
        if (enemy == null)
        {
            // The opening training battle has no garrison until a column arrives.
            enemy = new Vehicle
            {
                Id = Mission.FirstDefenderId, Enemy = true, Group = Mission.EnemyGroup,
                Home = new(70, -80), Kind = VehicleKind.T72, Position = new(70, -80), Waiting = true,
            };
            enemy.Rearm();
            Sim.Vehicles.Add(enemy);
        }
        // Feed a known presentation event so combat randomness cannot hide the lifecycle regression.
        battle.Hud.Report(new(CombatEventKind.Destroy, enemy.Position, UnitId: enemy.Id, Shooter: gunner.Id), Sim.Elapsed);
        Check(FullSight.ShotCall is { } sight && sight.Text.StartsWith("KILL", StringComparison.Ordinal), "Full-screen kill call is populated");
        Check(GroupWindows()[0].ShotCall is { } group && group.Text.StartsWith("KILL", StringComparison.Ordinal), "Group kill call is populated even while its window is hidden");
        if (temporaryTarget)
        {
            // This is a presentation fixture, not a reinforcement to persist in a checkpoint.
            Sim.Vehicles.Remove(enemy);
        }
    }

    private void CheckFreshViewState()
    {
        Check(battle.CameraRig.Camera.Environment == null, "Replacement battle clears the camera override immediately");
        Check(FullSight.ShotCall == null && GroupWindows().All(w => w.ShotCall == null), "Replacement battle clears every sight callout immediately");
        Check(GroupWindows().All(w => WindowViewport(w).GetCamera3D().Environment == null), "Hidden group cameras release their previous thermal environment");
    }

    private void CheckGroupSightEnvironments(bool night)
    {
        foreach (var window in GroupWindows())
        {
            var viewport = WindowViewport(window);
            bool sight = window.Mode == WindowMode.Sight;
            Check((viewport.GetCamera3D().Environment != null) == (night && sight), "Group thermal filter matches the current battle and window mode");
            Check((viewport.RenderTargetUpdateMode == SubViewport.UpdateMode.Always) == sight, "Only active sight windows render");
        }
    }

    private static SubViewport WindowViewport(GroupWindow window) =>
        window.FindChildren("*", nameof(SubViewport), true, false).OfType<SubViewport>().Single();

    private void CaptureViewState(string name)
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }
        RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, Callable.From(() =>
        {
            var path = CampaignStorage.QaPath($"view-state-{name}.png");
            var error = GetViewport().GetTexture().GetImage().SavePng(path);
            if (error != Error.Ok)
            {
                GD.PushError($"VIEW STATE capture failed: {error} {path}");
                GetTree().Quit(1);
            }
        }), (uint)ConnectFlags.OneShot);
    }
}
