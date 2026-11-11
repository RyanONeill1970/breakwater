namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>
/// Scene root and controller: owns the simulation, turns input and HUD commands into orders, and
/// drives the campaign flow (deploy, retry, continue, save/load). Settings handling is in
/// <c>Battle.Settings.cs</c>; scripted runtime checks live in <see cref="QaDriver"/>.
/// </summary>
public partial class Battle : Node3D, IBattleCommands
{
    /// <summary>Radians of turret traverse per pixel of mouse movement in the gunner sight, before magnification.</summary>
    private const float SightMouseRadians = .004f;

    // Created in _Ready, which Godot runs before any input, process or HUD callback.
    private BattleAudio audio = null!;
    private CameraRig cameraRig = null!;
    private CommandHud hud = null!;
    private BattleView view = null!;
    private LaunchOptions options = null!;
    private CampaignStorage storage = null!;
    private Simulation sim = new();

    private bool middleDrag;

    /// <summary>The four group windows are up (Classic mode).</summary>
    private bool windows;

    /// <summary>The artillery shell the next ground order calls down, or null for a normal move order.</summary>
    private ArtilleryShell? artillery;

    internal ArtilleryShell? PendingArtillery => artillery;

    // Gunner sight: mouse traverse not yet applied (the turret slews at its own rate), and whose sight it was last frame.
    private float pendingAim;
    private int lastGunner = -1;
    private bool newCampaignPending;
    private bool restartPending;
    private int saveSlot;
    private int selected;

    // Read and driven by QaDriver.
    internal BattleAudio Audio => audio;
    internal CameraRig CameraRig => cameraRig;
    internal BattleView Scene => view;
    internal CommandHud Hud => hud;
    internal int SaveSlot => saveSlot;
    internal string SavePath => storage.SlotPath(saveSlot);
    internal CampaignStorage Storage => storage;
    internal int Selected { get => selected; set => selected = value; }
    internal Simulation Sim => sim;

    internal CameraView View => windows ? CameraView.Windows : cameraRig.Tactical ? CameraView.Tactical : cameraRig.Sight ? CameraView.Sight : CameraView.Chase;

    /// <summary>
    /// The player is laying the selected group leader's gun (Classic mode, in battle): through the
    /// full-screen sight, or through that group's window showing its sight.
    /// </summary>
    internal bool GunSight => sim.Classic && sim.Phase == MissionPhase.Active &&
        (View == CameraView.Sight || (View == CameraView.Windows && hud.WindowModeOf(selected) == WindowMode.Sight));

    public override void _Ready()
    {
        options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        storage = new CampaignStorage(options.IsQa);
        LoadSettings();
        settings.InstallBindings();
        saveSlot = settings.SaveSlot;

        view = new BattleView();
        AddChild(view);
        view.Build(sim);
        cameraRig = new CameraRig();
        AddChild(cameraRig);
        audio = new BattleAudio();
        AddChild(audio);
        retroFilter = new RetroFilter();
        AddChild(retroFilter);
        var layer = new CanvasLayer();
        AddChild(layer);
        hud = new CommandHud { Commands = this, Settings = settings };
        layer.AddChild(hud);
        InitializeSettingsPanel();

        if (options.Island != null || options.Classic)
        {
            var campaign = Campaign.Start(options.Classic ? GameMode.Classic : GameMode.Modern);
            campaign.Island = options.Island ?? 0;
            if (campaign.Mode == GameMode.Classic)
            {
                campaign.Battle = Math.Min(options.Battle ?? 0, ClassicCampaign.FinalBattle(campaign.Island));
            }

            ReplaceSimulation(new Simulation(campaign));
        }

        if (options.IsQa)
        {
            var qa = new QaDriver();
            AddChild(qa);
            qa.Start(this, options);
        }

        cameraRig.Update(1, sim.Leader(selected));
        RefreshHud();
        if (settingsLoadWarning != null)
        {
            hud.Notify(settingsLoadWarning);
        }
    }

    public override void _UnhandledInput(InputEvent evt)
    {
        if (settingsPanel.Visible)
        {
            return;
        }

        switch (evt)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key);
                break;
            case InputEventMouseButton mouse:
                HandleMouseButton(mouse);
                break;
            case InputEventMouseMotion motion when middleDrag && cameraRig.Tactical:
                cameraRig.Drag(motion.Relative, settings.CameraSpeed);
                break;
            case InputEventMouseMotion motion when View == CameraView.Sight && Input.MouseMode == Input.MouseModeEnum.Captured:
                pendingAim -= motion.Relative.X * SightMouseRadians / (cameraRig.SightZoomed ? CameraRig.SightMagnification : 1);
                break;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float drive = Held(CommandIds.Forward) - Held(CommandIds.Reverse);
        float turn = Held(CommandIds.Left) - Held(CommandIds.Right);
        LayGun((float)delta, ref turn);
        sim.Step((float)delta, sim.Leader(selected)?.Id ?? -1, drive, turn);
        while (sim.Events.TryDequeue(out var evt))
        {
            view.Effect(evt);
            if (settings.CameraShake)
            {
                cameraRig.Shake(evt, sim.Leader(selected));
            }

            audio.PlayEvent(evt);
            hud.Report(evt, sim.Elapsed);
            if (evt.PlayerVisible && evt.Message.Length > 0)
            {
                hud.Notify(evt.Message);
            }
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (cameraRig.Tactical && !settingsPanel.Visible)
        {
            var pan = new Vector2(Held(CommandIds.PanRight) - Held(CommandIds.PanLeft), Held(CommandIds.PanDown) - Held(CommandIds.PanUp));
            cameraRig.Pan(pan, dt, settings.CameraSpeed);
        }

        if (!Input.IsMouseButtonPressed(MouseButton.Middle))
        {
            middleDrag = false;
        }

        var mouseMode = GunSight && View == CameraView.Sight && !sim.Paused && !settingsPanel.Visible ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        if (Input.MouseMode != mouseMode)
        {
            Input.MouseMode = mouseMode;
        }

        cameraRig.Night = sim.Night;
        cameraRig.Update(dt, sim.Leader(selected));
        view.Sync(sim, selected, sim.Paused ? 0 : dt);
        RefreshHud();
        audio.SetListener(cameraRig.Tactical ? cameraRig.Focus : BattleView.World(sim.Leader(selected)?.Position ?? V2.Zero));
        audio.SetPaused(sim.Paused);
    }

    public void SelectGroup(int group) => selected = group;

    /// <summary>Tactical, then chase, then (in Classic mode) the gunner sight and the four group windows, then back to tactical.</summary>
    public void ToggleView()
    {
        var next = View switch
        {
            CameraView.Tactical => CameraView.Chase,
            CameraView.Chase when sim.Classic => CameraView.Sight,
            CameraView.Sight when sim.Classic => CameraView.Windows,
            _ => CameraView.Tactical,
        };
        ShowView(next);
    }

    public void Maximise(int group, WindowMode mode)
    {
        selected = group;
        if (mode == WindowMode.Sight)
        {
            ShowView(CameraView.Sight);
            return;
        }

        ShowView(CameraView.Tactical);
        if (sim.Leader(group) is Vehicle leader)
        {
            cameraRig.FocusOn(leader.Position);
        }
    }

    public void FireGunner()
    {
        if (GunSight && sim.Leader(selected) is Vehicle gunner)
        {
            sim.FireGunner(gunner);
        }
    }

    public void CycleAmmunition()
    {
        if (GunSight && sim.Leader(selected) is Vehicle gunner && sim.CycleAmmunition(gunner) is Weapon loaded)
        {
            hud.Notify($"Loading {WeaponSpec.For(loaded).Name}: {WeaponSpec.For(loaded).Reload:0.#} seconds.");
        }
    }

    public void Smoke()
    {
        if (sim.Classic)
        {
            sim.FireSmoke(selected);
        }
    }

    public void LayMine()
    {
        if (sim.Classic && !sim.LayMine(selected))
        {
            hud.Notify("No mines left.");
        }
    }

    public void Artillery()
    {
        if (!sim.Classic || !sim.CanOrder)
        {
            return;
        }

        if (sim.ArtilleryLeft == 0)
        {
            hud.Notify("No artillery missions left.");
            return;
        }

        artillery = artillery switch
        {
            null => ArtilleryShell.HighExplosive,
            ArtilleryShell.HighExplosive => ArtilleryShell.Cluster,
            ArtilleryShell.Cluster => ArtilleryShell.Smoke,
            _ => null,
        };
        string key = settings.Hint(CommandIds.Artillery);
        hud.Notify(artillery is ArtilleryShell shell
            ? $"Artillery ({sim.ArtilleryLeft} left): {Simulation.ShellName(shell)}. Right-click the ground or map to fire; {key} changes the shell."
            : "Artillery mission cancelled.");
    }

    public void OrderGroup(int group, V2 point)
    {
        selected = group;
        GiveOrder(point);
    }

    internal void ShowView(CameraView view)
    {
        windows = view == CameraView.Windows;
        // The opaque group-window panel covers the main view. Do not draw the island a fourth time.
        cameraRig.Camera.CullMask = windows ? 0u : (1u << 20) - 1;
        cameraRig.Tactical = view is CameraView.Tactical or CameraView.Windows;
        cameraRig.Sight = view == CameraView.Sight;
    }

    public void Stop() => sim.Stop(selected);

    public void Regroup() => sim.Regroup(selected);

    public void ToggleFire() => sim.ToggleFire(selected);

    public void MapClick(V2 point, bool right)
    {
        if (right)
        {
            GiveOrder(point);
        }
        else
        {
            cameraRig.FocusOn(point);
            cameraRig.Tactical = true;
        }
    }

    public void Deploy()
    {
        if (sim.Phase != MissionPhase.Briefing)
        {
            return;
        }

        bool saved = WriteAutosave();
        sim.Begin();
        restartPending = false;
        newCampaignPending = false;
        if (sim.Classic)
        {
            // Classic battles are fought from the gunners' seats: three sights and a map to command from.
            for (int g = 0; g < Mission.GroupCount; g++)
            {
                hud.SetWindowMode(g, g == Mission.GroupCount - 1 ? WindowMode.Map : WindowMode.Sight);
            }

            ShowView(CameraView.Windows);
        }

        if (saved)
        {
            hud.Notify(sim.Classic
                ? "Task force deployed. Click a sight to take that gun, lay it with the turn keys and click to fire; your crews shoot badly on their own. Tab changes view."
                : "Task force deployed. Right-click ground or the minimap to move.");
        }
    }

    public void Configure(int group, DeployAction action)
    {
        if (sim.Phase != MissionPhase.Briefing)
        {
            return;
        }

        var campaign = sim.Campaign.Clone();
        bool changed = action switch
        {
            DeployAction.Refit => campaign.Refit(group),
            DeployAction.Repair => campaign.Repair(group),
            DeployAction.Replace => campaign.Replace(group),
            _ => false,
        };
        if (!changed)
        {
            hud.Notify("No change: check available supplies and group condition.");
            return;
        }

        ReplaceSimulation(new Simulation(campaign));
        hud.Notify($"{Mission.GroupNames[group]} prepared. {Campaign.Money(campaign.Supplies, campaign.Mode)} remaining.");
    }

    public void ContinueCampaign()
    {
        if (sim.Phase != MissionPhase.Victory || Mission.FinalBattle(sim.Campaign))
        {
            return;
        }

        ReplaceSimulation(sim.NextIsland());
        if (WriteAutosave())
        {
            hud.Notify("Survivors transferred and autosaved. Prepare your force, then deploy.");
        }
    }

    public void NewCampaign() => StartCampaign(sim.Campaign.Mode, "New campaign");

    public void Quit() => GetTree().Quit();

    public void SwitchMode()
    {
        var other = sim.Campaign.Mode == GameMode.Classic ? GameMode.Modern : GameMode.Classic;
        StartCampaign(other, CommandHud.ModeButtonText(sim.Campaign.Mode));
    }

    /// <summary>
    /// Starts a fresh campaign in <paramref name="mode"/>. Unless the current campaign is untouched or
    /// complete, the first press only asks the player to press <paramref name="button"/> again.
    /// </summary>
    private void StartCampaign(GameMode mode, string button)
    {
        bool campaignComplete = sim.Phase == MissionPhase.Victory && Mission.FinalBattle(sim.Campaign);
        bool untouched = sim.Phase == MissionPhase.Briefing && sim.Campaign.Island == 0 && sim.Campaign.Battle == 0 && sim.Campaign.Supplies == Campaign.Start(sim.Campaign.Mode).Supplies;
        if (!newCampaignPending && !campaignComplete && !untouched)
        {
            newCampaignPending = true;
            hud.Notify($"Click {button} again to discard this campaign and start over.");
            return;
        }

        ReplaceSimulation(new Simulation(Campaign.Start(mode)));
        hud.Notify($"New {mode} campaign ready. Saved checkpoints have not been changed.");
    }

    public void TogglePause()
    {
        if (sim.Phase == MissionPhase.Active)
        {
            sim.Paused = !sim.Paused;
        }
    }

    /// <summary>
    /// Retry the island. Immediate after a defeat; during combat it needs a second press. After a
    /// victory it does nothing: a stray key press must not throw away a won island.
    /// </summary>
    public void Restart()
    {
        switch (sim.Phase)
        {
            case MissionPhase.Defeat:
            case MissionPhase.Active when restartPending:
                ReplaceSimulation(sim.RetryIsland());
                break;
            case MissionPhase.Active:
                restartPending = true;
                hud.Notify($"Press {settings.Hint(CommandIds.Restart)} or Restart again to discard this mission and restart.");
                break;
            case MissionPhase.Victory:
                hud.Notify("Island secured. Continue to the next island, or load a checkpoint to replay.");
                break;
        }
    }

    public void Save()
    {
        try
        {
            CampaignStorage.Save(sim, SavePath);
            hud.Notify($"Campaign saved in slot {saveSlot + 1}. {settings.Hint(CommandIds.Load)} restores this checkpoint.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            hud.Notify("Save unavailable: " + ex.Message);
        }
    }

    public void Load() => LoadCheckpoint(SavePath);

    public void LoadAutosave() => LoadCheckpoint(storage.AutosavePath);

    public void CycleSaveSlot()
    {
        saveSlot = (saveSlot + 1) % CampaignStorage.SlotCount;
        settings.SaveSlot = saveSlot;
        hud.Notify($"Slot {saveSlot + 1}: {settings.Hint(CommandIds.Save)} saves; {settings.Hint(CommandIds.Load)} loads; {settings.Hint(CommandIds.Slot)} changes slot.");
        PersistQuickSettings();
    }

    private void HandleKey(InputEventKey key)
    {
        if (key.Keycode == Key.F1)
        {
            OpenSettings();
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            TogglePause();
            return;
        }

        string? command = GameSettings.Commands.FirstOrDefault(c => key.IsActionPressed(GameSettings.ActionName(c.Id)))?.Id;
        int group = CommandIds.GroupOf(command);
        if (group >= 0)
        {
            selected = group;
            return;
        }

        switch (command)
        {
            case CommandIds.Deploy:
                Deploy();
                break;
            case CommandIds.View:
                ToggleView();
                break;
            case CommandIds.Pause:
                TogglePause();
                break;
            case CommandIds.Restart:
                Restart();
                break;
            case CommandIds.Stop:
                Stop();
                break;
            case CommandIds.Regroup:
                Regroup();
                break;
            case CommandIds.Hold:
                ToggleFire();
                break;
            case CommandIds.Center:
                if (sim.Leader(selected) is Vehicle leader)
                {
                    cameraRig.FocusOn(leader.Position);
                }
                break;
            case CommandIds.Mute:
                ToggleMute();
                break;
            case CommandIds.Save:
                Save();
                break;
            case CommandIds.Slot:
                CycleSaveSlot();
                break;
            case CommandIds.Load:
                Load();
                break;
            case CommandIds.Autosave:
                LoadAutosave();
                break;
            case CommandIds.Smoke:
                Smoke();
                break;
            case CommandIds.Artillery:
                Artillery();
                break;
            case CommandIds.Mine:
                LayMine();
                break;
            case CommandIds.Fire:
                if (sim.Leader(selected) is Vehicle tank)
                {
                    _ = GunSight ? sim.FireGunner(tank) : sim.TryFire(tank);
                }
                break;
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouse)
    {
        if (mouse.ButtonIndex == MouseButton.Middle)
        {
            middleDrag = mouse.Pressed;
        }

        if (!mouse.Pressed)
        {
            return;
        }

        if (View == CameraView.Windows)
        {
            // The windows handle their own clicks; the ground behind them takes no orders.
            return;
        }

        if (View == CameraView.Sight && GunSight)
        {
            SightMouseButton(mouse.ButtonIndex);
            return;
        }

        switch (mouse.ButtonIndex)
        {
            case MouseButton.WheelUp:
                cameraRig.ZoomIn();
                break;
            case MouseButton.WheelDown:
                cameraRig.ZoomOut();
                break;
            case MouseButton.Right when cameraRig.GroundPoint(mouse.Position) is V2 point:
                GiveOrder(point);
                break;
            case MouseButton.Left when cameraRig.GroundPoint(mouse.Position) is V2 clicked:
                SelectVehicleAt(clicked);
                break;
        }
    }

    /// <summary>In the gunner sight, the mouse fires, changes ammunition and zooms instead of commanding.</summary>
    private void SightMouseButton(MouseButton button)
    {
        if (sim.Leader(selected) is not Vehicle gunner)
        {
            return;
        }

        switch (button)
        {
            case MouseButton.Left:
                sim.FireGunner(gunner);
                break;
            case MouseButton.Right:
                CycleAmmunition();
                break;
            case MouseButton.WheelUp:
                cameraRig.SightZoomed = true;
                break;
            case MouseButton.WheelDown:
                cameraRig.SightZoomed = false;
                break;
        }
    }

    /// <summary>
    /// Hands the selected leader's turret to the player while the gunner sight is up: the turn keys
    /// and the mouse traverse it, and the hull holds its heading. When the leader is lost, the sight
    /// passes to the next vehicle in the group.
    /// </summary>
    private void LayGun(float dt, ref float turn)
    {
        var gunner = GunSight ? sim.Leader(selected) : null;
        float traverse = 0;
        if (gunner != null && !sim.Paused)
        {
            float slew = gunner.Spec.TurretTurn * dt;
            float mouse = Math.Clamp(pendingAim / slew, -1, 1);
            pendingAim -= mouse * slew;
            traverse = Math.Clamp(turn + mouse, -1, 1);
            turn = 0;
            if (gunner.Id != lastGunner && Mission.GroupOf(lastGunner) == selected && sim.Vehicles.Any(v => v.Id == lastGunner && !v.Alive))
            {
                hud.Notify($"{Mission.GroupNames[selected]} leader lost: the sight passes to the next {gunner.Spec.Name}.");
            }
        }

        if (gunner == null)
        {
            pendingAim = 0;
        }

        sim.GunnerId = gunner?.Id ?? -1;
        sim.GunnerTraverse = traverse;
        lastGunner = gunner?.Id ?? -1;
    }

    private void SelectVehicleAt(V2 point)
    {
        // A click within a hull-and-a-bit of a friendly vehicle selects its group.
        const float pickRadius = 5;
        var closest = sim.Vehicles.Where(v => v.Player && v.Alive).MinBy(v => V2.DistanceSquared(v.Position, point));
        if (closest != null && V2.Distance(closest.Position, point) < pickRadius)
        {
            selected = closest.Group;
        }
    }

    private void GiveOrder(V2 point)
    {
        if (settingsPanel.Visible || !sim.CanOrder)
        {
            return;
        }

        if (!Navigation.Inside(point))
        {
            hud.Notify("Order rejected: choose ground inside the island.");
            return;
        }

        if (artillery is ArtilleryShell shell)
        {
            artillery = null;
            sim.CallArtillery(point, shell);
            return;
        }

        sim.Order(selected, point, Input.IsPhysicalKeyPressed(Key.Shift));
    }

    internal void ReplaceSimulation(Simulation replacement)
    {
        sim = replacement;
        RemoveChild(view);
        view.QueueFree();
        view = new BattleView();
        AddChild(view);
        view.Build(sim);
        selected = 0;
        ShowView(CameraView.Tactical);
        artillery = null;
        cameraRig.Reset();
        cameraRig.Night = sim.Night;
        restartPending = false;
        newCampaignPending = false;
        cameraRig.Update(1, sim.Leader(selected));
        RefreshHud();
    }

    private void RefreshHud() => hud.Refresh(sim, cameraRig.Camera, selected, View, cameraRig.SightZoomed, audio.Muted, saveSlot);

    private void LoadCheckpoint(string path)
    {
        try
        {
            ReplaceSimulation(CampaignStorage.Load(path));
            hud.Notify(sim.Phase == MissionPhase.Active
                ? $"Checkpoint restored and paused. Press {settings.Hint(CommandIds.Pause)} when ready."
                : "Campaign checkpoint restored.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            hud.Notify("Load unavailable: " + ex.Message);
        }
    }

    private bool WriteAutosave()
    {
        try
        {
            CampaignStorage.Save(sim, storage.AutosavePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            hud.Notify("Autosave unavailable: " + ex.Message);
            return false;
        }
    }
}
