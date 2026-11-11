namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>The minimap: terrain, fog, sites, visible vehicles, the selected group's orders and the camera focus.</summary>
public partial class TacticalMap : Control
{
    /// <summary>World metres shown across the map: the island plus a strip of sea.</summary>
    private const float WorldSpan = 200;

    /// <summary>Fraction of the map inset on each side before the land starts.</summary>
    private const float LandInset = .04f;

    private const int GridDivisions = 4;
    private const float VehicleDot = 2.1f, SelectedDot = 3.3f, WaypointDot = 3, GhostDot = 3.7f;
    private const float SiteRing = .08f;
    private const float Crosshair = 6;

    // Enemy moves ping the map: an expanding ring that fades, repeated a few times.
    private const float PingSeconds = 4;
    private const float PingCycles = 3;
    private const float PingRadius = 18;

    /// <summary>Threatened sites' rings pulse at this rate, in cycles per second.</summary>
    private const float ThreatPulseRate = 1.5f;

    // Classic support weapons: smoke screens are translucent discs, the player's mines small dots.
    private const float MapSmokeAlpha = .45f;
    private const float MineDot = 1.6f;

    private readonly List<(V2 Position, float Time)> pings = [];
    private Simulation? simulation;

    // Smoothed ground and fog textures, redrawn when the island or the exploration changes.
    private TerrainMap? groundOf;
    private ImageTexture? ground, fog;
    private Reconnaissance? fogOf;
    private int fogRevision = -1;

    public Action<V2, bool>? Click { get; set; }
    public V2 CameraPosition { get; set; }
    public int Selected { get; set; }
    public Simulation? Simulation
    {
        get => simulation;
        set
        {
            if (simulation != value)
            {
                pings.Clear();
            }
            simulation = value;
        }
    }

    public override void _GuiInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton { Pressed: true } b && b.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            var p = (b.Position / Size - Vector2.One * .5f) * WorldSpan;
            Click?.Invoke(new(p.X, p.Y), b.ButtonIndex == MouseButton.Right);
            AcceptEvent();
        }
    }

    /// <summary>Pings the map where an intercepted enemy move is heading or landing.</summary>
    public void Report(CombatEvent evt, float time)
    {
        if (evt.PlayerVisible && evt.Kind is CombatEventKind.Assault or CombatEventKind.Reinforcement or CombatEventKind.SiteLost or CombatEventKind.IncomingArtillery)
        {
            pings.Add((evt.Position, time));
        }
    }

    public override void _Ready() => TextureFilter = TextureFilterEnum.LinearWithMipmaps;

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Palette.Map.Sea);
        DrawRect(new Rect2(Size * LandInset, Size * (1 - 2 * LandInset)), Palette.Map.Land);
        for (int i = 1; i < GridDivisions; i++)
        {
            DrawLine(new(Size.X * i / GridDivisions, 0), new(Size.X * i / GridDivisions, Size.Y), Palette.Map.Grid);
            DrawLine(new(0, Size.Y * i / GridDivisions), new(Size.X, Size.Y * i / GridDivisions), Palette.Map.Grid);
        }

        if (Simulation == null)
        {
            return;
        }

        // The exploration grid's square on the map, which the ground and fog textures fill.
        var gridCorner = new V2(Reconnaissance.GridOrigin - Reconnaissance.CellSize / 2);
        var grid = new Rect2(Map(gridCorner), MapSize(new V2(Reconnaissance.GridSize * Reconnaissance.CellSize)));
        if (Simulation.Terrain is TerrainMap terrain)
        {
            if (groundOf != terrain)
            {
                groundOf = terrain;
                ground = ImageTexture.CreateFromImage(GridImage.Ground(terrain, Palette.Map.Ground));
            }

            DrawTextureRect(ground, grid, false);
        }

        foreach (var o in Simulation.Obstacles)
        {
            DrawRect(new Rect2(Map(o.Center - o.HalfSize), MapSize(o.HalfSize * 2)), Palette.Map.Obstacle);
        }

        var recon = Simulation.Recon;
        if (fogOf != recon || fogRevision != recon.Revision)
        {
            fogOf = recon;
            fogRevision = recon.Revision;
            fog = GridImage.Show(fog, GridImage.Fog(recon, Palette.MapFogUnexplored, Palette.FogExplored));
        }

        DrawTextureRect(fog, grid, false);

        float now = Simulation.Elapsed;
        for (int i = 0; i < Simulation.Sites.Count; i++)
        {
            var site = Simulation.Sites[i];
            var ring = site.Secured ? Palette.Ui.Accent : Palette.Map.Site;
            if (Simulation.Threatened(i))
            {
                ring = ring.Lerp(Palette.Ui.Alert, .5f + .5f * Mathf.Sin(now * ThreatPulseRate * Mathf.Tau));
            }

            DrawCircle(Map(site.Position), Size.X * SiteRing, ring, false, Simulation.Threatened(i) ? 2 : 1, true);
        }

        foreach (var cloud in Simulation.Smoke)
        {
            DrawCircle(Map(cloud.Position), MapSize(new V2(cloud.Radius)).X, Palette.World.SmokeScreen with { A = MapSmokeAlpha }, antialiased: true);
        }

        foreach (var mine in Simulation.Mines.Where(m => !m.Enemy))
        {
            DrawCircle(Map(mine.Position), MineDot, Palette.World.Mine, antialiased: true);
        }

        foreach (var strike in Simulation.Strikes)
        {
            var mark = Map(strike.Target);
            var color = strike.Enemy ? Palette.Ui.Alert : Palette.World.FireMission;
            DrawLine(mark - new Vector2(Crosshair, Crosshair), mark + new Vector2(Crosshair, Crosshair), color, 2, true);
            DrawLine(mark - new Vector2(Crosshair, -Crosshair), mark + new Vector2(Crosshair, -Crosshair), color, 2, true);
        }

        pings.RemoveAll(p => now - p.Time > PingSeconds || now < p.Time);
        foreach (var (position, time) in pings)
        {
            float phase = (now - time) / PingSeconds * PingCycles % 1;
            DrawCircle(Map(position), PingRadius * phase, Palette.Ui.Alert with { A = 1 - phase }, false, 2, true);
        }

        foreach (var tank in Simulation.Vehicles.Where(recon.IsVisible))
        {
            bool selected = tank.Alive && !tank.Enemy && tank.Group == Selected;
            var color = !tank.Alive ? Palette.Map.Wreck : Palette.Team(tank);
            DrawCircle(Map(tank.Position), selected ? SelectedDot : VehicleDot, color, antialiased: true);
            if (selected)
            {
                var last = tank.Position;
                foreach (var p in tank.Orders)
                {
                    DrawLine(Map(last), Map(p), color, 1, true);
                    DrawCircle(Map(p), WaypointDot, color, false, 1, true);
                    last = p;
                }
            }
        }

        foreach (var contact in recon.Ghosts)
        {
            DrawCircle(Map(contact.Position), GhostDot, Palette.Ghost, false, 1, true);
        }

        var focus = Map(CameraPosition);
        if (new Rect2(Vector2.Zero, Size).HasPoint(focus))
        {
            DrawLine(focus - new Vector2(Crosshair, 0), focus + new Vector2(Crosshair, 0), Colors.White, 1);
            DrawLine(focus - new Vector2(0, Crosshair), focus + new Vector2(0, Crosshair), Colors.White, 1);
        }
    }

    private Vector2 Map(V2 p) => (new Vector2(p.X, p.Y) / WorldSpan + Vector2.One * .5f) * Size;

    private Vector2 MapSize(V2 size) => new Vector2(size.X, size.Y) / WorldSpan * Size;
}
