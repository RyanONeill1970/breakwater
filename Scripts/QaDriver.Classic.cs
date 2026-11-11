namespace Breakwater;

using V2 = System.Numerics.Vector2;

// The Classic-mode check: switches rules from the briefing, uses the gunner sight's keys and mouse,
// then fights long enough for the terrain, jungle scenery and ammunition readout to be exercised.
public partial class QaDriver
{
    private const int ClassicSwitchFrame = 5;
    private const int ClassicDeployFrame = 20;
    private const int ClassicSightFrame = 60;
    private const int ClassicCheckFrame = 900;

    private void ClassicCheck()
    {
        switch (frame)
        {
            case ClassicSwitchFrame:
                Click(HudButton(CommandHud.ModeButtonText(GameMode.Modern)));
                break;
            case ClassicDeployFrame:
                Check(Sim.Classic && Sim.Terrain != null && Sim.Campaign.Roster[0].Kind == VehicleKind.Abrams, "Mode button starts a Classic campaign");
                Check(battle.FindChildren("*", nameof(MultiMeshInstance3D), true, false).Count > 0, "Jungle scenery built");
                Check(HudButton(CommandHud.ModeButtonText(GameMode.Classic)) != null, "Mode button offers Modern rules");
                Check(battle.Hud.FindChildren("*", nameof(Label), true, false).OfType<Label>().Any(l => l.Text.Contains("FUNDS") && l.Text.Contains("$55M")), "Classic funds in dollars");
                Click(HudButton("Deploy task force"));
                break;
            case ClassicDeployFrame + 1:
                Check(Sim.Phase == MissionPhase.Active, "Classic deploy");
                Check(battle.View == CameraView.Windows && battle.Hud.WindowModeOf(Mission.GroupCount - 1) == WindowMode.Map, "Classic battles open in the group windows");
                Check(battle.CameraRig.Camera.CullMask == 0, "Covered main camera skips world rendering");
                KeyPress(Key.Tab);
                for (int g = 0; g < Mission.GroupCount; g++)
                {
                    Sim.Order(g, Sim.Sites[0].Position + new V2((g - 1.5f) * 9, 6));
                }
                break;
            case ClassicSightFrame:
                Check(battle.View == CameraView.Tactical, "View key leaves the group windows for the map");
                Check(battle.CameraRig.Camera.CullMask != 0, "Tactical world rendering restored");
                KeyPress(Key.Tab);
                break;
            case ClassicSightFrame + 1:
                KeyPress(Key.Tab);
                break;
            case ClassicSightFrame + 2:
                Check(battle.GunSight && Sim.GunnerId == Sim.Leader(battle.Selected)?.Id, "View key reaches the gunner sight");
                Sim.Leader(battle.Selected)!.Reload = 0;
                SightClick(MouseButton.Right);
                break;
            case ClassicSightFrame + 3:
                var gunner = Sim.Leader(battle.Selected)!;
                Check(gunner.Loaded != Weapon.Sabot && gunner.Reload > 0, "Right-click changes ammunition");
                gunner.Reload = 0;
                int shots = Sim.ShotsFired;
                SightClick(MouseButton.Left);
                Check(Sim.ShotsFired == shots + 1, "Click fires the gun");
                KeyPress(Key.Tab);
                break;
            case ClassicSightFrame + 4:
                Check(battle.View == CameraView.Windows && GroupWindows().Count == Mission.GroupCount, "View key opens the group windows");
                Click(ModeButton(GroupWindows()[1], WindowMode.Map));
                break;
            case ClassicSightFrame + 5:
                Check(battle.Selected == 1 && battle.Hud.WindowModeOf(1) == WindowMode.Map, "A window's map button selects its group");
                Sim.Stop(1);
                Click(GroupWindows()[1].FindChildren("*", nameof(Control), true, false).OfType<TacticalMap>().Single(), MouseButton.Right);
                break;
            case ClassicSightFrame + 6:
                Check(Sim.Group(1).Any(v => v.Orders.Count > 0), "Right-click on a window's map orders its group");
                Check(!battle.GunSight, "No gun is laid by hand while the selected window shows its map");
                KeyPress(Key.Tab);
                break;
            case ClassicSightFrame + 7:
                Check(battle.View == CameraView.Tactical && Sim.GunnerId == -1, "View key returns to the tactical map");
                KeyPress(Key.C);
                KeyPress(Key.N);
                KeyPress(Key.B);
                break;
            case ClassicSightFrame + 8:
                Check(Sim.Smoke.Count > 0, "Smoke key");
                Check(Sim.Mines.Count(m => !m.Enemy) == 1 && Sim.MinesLeft == Simulation.PlayerMines - 1, "Mine key");
                Check(battle.PendingArtillery == ArtilleryShell.HighExplosive, "Artillery key readies a mission");
                Click(battle.Hud.FindChildren("*", nameof(Control), true, false).OfType<TacticalMap>().Single(m => m.IsVisibleInTree()), MouseButton.Right);
                break;
            case ClassicSightFrame + 9:
                Check(Sim.Strikes.Count(s => !s.Enemy) == 1 && Sim.ArtilleryLeft == Simulation.PlayerArtilleryMissions - 1 && battle.PendingArtillery == null, "Right-click fires the mission");
                break;
            case ClassicCheckFrame:
                bool valid = Sim.Vehicles.All(v => Geometry.Finite(v.Position) && Sim.Navigation.Clear(v.Position, v.Position, Mission.VehicleRadius));
                Check(valid, "Classic vehicle positions");
                Check(Sim.Vehicles.All(v => v.Spec.Classic), "Only Classic vehicles on a Classic island");
                Check(battle.Hud.FindChildren("*", nameof(Label), true, false).OfType<Label>().Any(l => l.Text.Contains(WeaponSpec.For(Weapon.Sabot).Name)), "Ammunition readout");
                Pass($"CLASSIC PASS: mode switch, terrain and jungle scenery, gunner sight, group windows, smoke, mines and artillery, funds, {Sim.ShotsFired} shots in {Sim.Elapsed:0.0}s of Classic combat.");
                break;
        }
    }

    private List<GroupWindow> GroupWindows() => battle.Hud.FindChildren("*", nameof(PanelContainer), true, false).OfType<GroupWindow>().ToList();

    private static Button ModeButton(GroupWindow window, WindowMode mode) =>
        window.FindChildren("*", nameof(Button), true, false).OfType<Button>().First(b => b.Text == mode.ToString().ToUpperInvariant());

    /// <summary>A mouse click in the middle of the screen, where the sight's crosshair is.</summary>
    private void SightClick(MouseButton button)
    {
        var p = GetViewport().GetVisibleRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, GlobalPosition = p, Position = p, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, GlobalPosition = p, Position = p, Pressed = false }, true);
    }
}
