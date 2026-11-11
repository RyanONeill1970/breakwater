namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>
/// Procedural 3D presentation of a <see cref="Simulation"/>: terrain, cover, vehicles, shells,
/// effects, route lines and the fog overlay. Simulation X/Y map to Godot X/Z.
/// </summary>
public partial class BattleView : Node3D
{
    // Drawing heights above the ground plane, in metres, chosen to avoid z-fighting.
    private const float RouteHeight = .18f;
    private const float FogHeight = .085f;
    private const float ShellHeight = 1.45f;


    private const float SeaLevel = -1.05f;

    // Haze fades the world towards the horizon colour between these camera depths, in metres.
    private const float HazeStart = 260, HazeEnd = 1100;

    // Ambient occlusion strength and reach in metres, bloom strength, and exposure under the filmic tone curve.
    private const float SsaoIntensity = 1.6f, SsaoRadius = 1.4f, GlowIntensity = .2f, Exposure = .8f;

    /// <summary>How much the haze veils the sky itself (0–1).</summary>
    private const float HazeOnSky = .35f;

    // Photographed skies (Poly Haven, CC0): a partly cloudy day, and a clear night.
    private const string DaySkyPath = "res://assets/sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr";
    private const string NightSkyPath = "res://assets/sky/qwantani_night_puresky_2k.hdr";

    /// <summary>How strongly the day sky lights the shade.</summary>
    private const float SkyAmbientEnergy = .75f;

    /// <summary>Only light brighter than this blooms: fire and flashes, not sunlit sand.</summary>
    private const float GlowThreshold = 1.2f;


    // Night: a low moon and a dim blue ambient, so the tactical map stays readable but the battlefield is dark.
    private const float MoonEnergy = .3f, NightAmbientEnergy = .45f;


    /// <summary>Each island's east–west road runs at this northing.</summary>
    private static readonly float[] RoadNorthing = [-37, -20, -4];



    private readonly Dictionary<int, Model> models = [];

    private const float TargetRingMargin = 3;

    /// <summary>The team marker floats this far above the top of its vehicle.</summary>
    private const float MarkerClearance = .6f;
    private MeshInstance3D? targetRing;

    /// <summary>The nodes drawing each obstacle, so a building knocked down in Classic mode can become rubble.</summary>
    private readonly Dictionary<Obstacle, Node3D> obstacles = [];

    // Rubble: this many broken blocks, up to this size in metres, scattered over the footprint.
    private const int RubblePieces = 9;
    private const float RubbleSize = 2.4f;
    private const int RubbleSeed = 7;
    private readonly Dictionary<int, MeshInstance3D> shells = [];

    // Shared by every shell rather than allocated per shot.
    private readonly BoxMesh shellMesh = new() { Size = new(.13f, .13f, 1.5f) };
    private readonly StandardMaterial3D shellMaterial = Material(Palette.World.Shell, true);

    // Rebuilt in place each frame / each recon refresh rather than reallocated.
    private readonly ImmediateMesh routeLines = new();
    private int fogRevision = -1;

    /// <summary>The fog overlay: one plane over the grid, textured from the exploration cells.</summary>
    private readonly StandardMaterial3D fogMaterial = GridMaterial(unshaded: true, FogPriority);
    private ImageTexture? fogTexture;

    /// <summary>A night battle: hulls glow faintly with engine heat, which the thermal sight picks out.</summary>
    private bool night;

    /// <summary>Presentation time: advances only while the battle runs, and drives cosmetic animation.</summary>
    private float clock;

    public static Vector3 World(V2 p, float height = 0) => new(p.X, height, p.Y);

    public static MeshInstance3D Box(Node parent, Vector3 pos, Vector3 size, Color color) =>
        Mesh(parent, pos, new BoxMesh { Size = size }, color);

    public void Build(Simulation sim)
    {
        night = sim.Night;
        scenerySim = sim;
        BuildEnvironment(sim.Campaign.Island, sim.Terrain == null, night);
        if (sim.Terrain != null)
        {
            BuildTerrain(sim.Terrain);
        }
        foreach (var obstacle in sim.Obstacles)
        {
            BuildObstacle(obstacle);
        }

        BuildIslandCover(sim);

        foreach (var site in sim.Sites)
        {
            BuildSite(site);
        }

        if (sim.TargetBuilding is Obstacle target)
        {
            // A ring round the building a Destroy battle must knock down.
            targetRing = Ring(this, World(target.Center, .12f), Math.Max(target.HalfSize.X, target.HalfSize.Y) + TargetRingMargin, Palette.Ui.Alert);
        }

        foreach (var tank in sim.Vehicles)
        {
            CreateTank(tank);
        }

        AddChild(new MeshInstance3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = Material(Palette.Ui.Accent, true),
            Mesh = routeLines,
        });
        AddChild(GridPlane(FogHeight, fogMaterial));
    }

    public void Sync(Simulation sim, int selected, float dt)
    {
        // A convoy vehicle that drove off the island has left the battle.
        foreach (int gone in models.Keys.Where(id => !sim.Vehicles.Any(v => v.Id == id)).ToList())
        {
            models[gone].Hull.QueueFree();
            models.Remove(gone);
            damageSmoke.Remove(gone);
        }

        foreach (var tank in sim.Vehicles)
        {
            SyncTank(sim, tank, selected);
        }

        clock += dt;
        SyncSites(sim);
        SyncShells(sim);
        SyncSupport(sim);
        SyncRubble(sim);
        if (targetRing != null)
        {
            targetRing.Visible = sim.TargetHealth > 0;
        }
        // Freezes every particle effect in the view while the battle is paused.
        ProcessMode = sim.Paused ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;

        DrawRoutes(sim, selected);
        if (fogRevision != sim.Recon.Revision)
        {
            fogRevision = sim.Recon.Revision;
            DrawFog(sim.Recon);
        }
    }


    private static MeshInstance3D Mesh(Node parent, Vector3 pos, Godot.Mesh mesh, Color color)
    {
        var node = new MeshInstance3D { MaterialOverride = Material(color), Mesh = mesh, Position = pos };
        parent.AddChild(node);
        return node;
    }

    private static StandardMaterial3D Material(Color color, bool unshaded = false) => new()
    {
        AlbedoColor = color,
        Roughness = .9f,
        ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
        Transparency = color.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
    };

    private static SphereMesh Sphere(float radius) => new() { Height = radius * 2, Radius = radius, RadialSegments = 8, Rings = 4 };

    private static MeshInstance3D Ring(Node parent, Vector3 pos, float radius, Color color)
    {
        const float halfWidth = .09f;
        var node = Mesh(parent, pos, new TorusMesh { InnerRadius = radius - halfWidth, OuterRadius = radius + halfWidth, RingSegments = 6, Rings = 40 }, color);
        node.MaterialOverride = Material(color, true);
        return node;
    }

    private void BuildEnvironment(int island, bool modernRoads, bool night)
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                // The study's soft blue fill by day; the photographed night sky under moonlight.
                AmbientLightEnergy = night ? NightAmbientEnergy : SkyAmbientEnergy,
                AmbientLightSource = night ? Godot.Environment.AmbientSource.Sky : Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Palette.Art.SkyFill,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
                BackgroundMode = Godot.Environment.BGMode.Sky,
                // Distance haze: the tactical camera sits closer than HazeStart, so only the chase view's horizon fades.
                FogDepthBegin = HazeStart,
                FogDepthEnd = HazeEnd,
                FogEnabled = true,
                FogLightColor = night ? Palette.Night.Haze : Palette.Art.Haze,
                FogMode = Godot.Environment.FogModeEnum.Depth,
                FogSkyAffect = HazeOnSky,
                Sky = new Sky
                {
                    SkyMaterial = new PanoramaSkyMaterial { Panorama = GD.Load<Texture2D>(night ? NightSkyPath : DaySkyPath) },
                },
                // Forward+ only: contact shadows in creases and under hulls, and a soft bloom on fire and sun glints.
                GlowEnabled = true,
                GlowHdrThreshold = GlowThreshold,
                GlowIntensity = GlowIntensity,
                SsaoEnabled = false,
                SsaoIntensity = SsaoIntensity,
                SsaoRadius = SsaoRadius,
                TonemapMode = night ? Godot.Environment.ToneMapper.Filmic : Godot.Environment.ToneMapper.Aces,
                TonemapExposure = night ? Exposure : 1,
            },
        });
        AddChild(new DirectionalLight3D
        {
            LightColor = night ? Palette.Night.Moon : Palette.Art.Sun,
            LightEnergy = night ? MoonEnergy : 1.35f,
            RotationDegrees = night ? new(-52, -35, 0) : new(-29, -42, 0),
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 390,
        });

        BuildIsland(island, modernRoads);
        Box(this, new(0, .035f, Mission.SupplyLineY + .5f), new(174, .07f, 1), Palette.World.SupplyLine);
    }
    /// <summary>Replaces each building the battle has knocked down with a low heap of rubble.</summary>
    private void SyncRubble(Simulation sim)
    {
        foreach (var fallen in obstacles.Keys.Where(o => !sim.Obstacles.Contains(o)).ToList())
        {
            obstacles[fallen].QueueFree();
            obstacles.Remove(fallen);
            var random = new Random(RubbleSeed + (int)fallen.Center.X * 31 + (int)fallen.Center.Y);
            for (int i = 0; i < RubblePieces; i++)
            {
                var spot = fallen.Center + new V2((random.NextSingle() * 2 - 1) * fallen.HalfSize.X, (random.NextSingle() * 2 - 1) * fallen.HalfSize.Y);
                float size = RubbleSize * (.5f + random.NextSingle());
                var piece = Box(this, World(spot, size / 4), new(size, size / 2, size * .8f), (i % 3 == 0 ? Palette.World.Roof : Palette.World.Building).Darkened(.25f));
                piece.RotationDegrees = new(0, random.NextSingle() * 90, 0);
            }
        }
    }

    private void CreateTank(Vehicle tank)
    {
        var body = Body(tank.Kind, tank.Enemy, tank.Convoy);
        AddChild(body.Hull);
        var team = Palette.Team(tank);
        var marker = Box(body.Hull, new(0, body.Height + MarkerClearance, 0), new(.8f, .08f, .6f), team);
        var selection = Ring(body.Hull, new(0, .1f, 0), Mission.VehicleRadius + .55f, team);
        models[tank.Id] = new(body.Hull, body.Turret, selection, marker, body.Muzzle);
    }

    /// <summary>
    /// A vehicle's hull with its turret on it: the downloaded model where there is one
    /// (<see cref="VehicleAssets"/>), otherwise the procedural one (<see cref="VehicleModel"/>).
    /// </summary>
    internal (Node3D Hull, Node3D Turret, Vector3 Muzzle, float Height) Body(VehicleKind kind, bool enemy, bool convoy)
    {
        var hull = new Node3D();
        if (VehicleAssets.For(kind, night) is { } model)
        {
            hull.AddChild(new MeshInstance3D { Mesh = model.Hull });
            var fitted = new Node3D { Position = model.Pivot };
            hull.AddChild(fitted);
            if (model.Turret != null)
            {
                fitted.AddChild(new MeshInstance3D { Mesh = model.Turret });
            }

            return (hull, fitted, model.Muzzle, model.Hull.GetAabb().End.Y);
        }

        var shape = Silhouette.Of(kind);
        var color = enemy ? Palette.World.EnemyHull : shape.Light || convoy ? Palette.World.CarrierHull : kind == VehicleKind.Scout ? Palette.World.ScoutHull : Palette.World.TankHull;
        var (hullMesh, turretMesh) = VehicleModel.Build(shape, color);
        var paint = VehicleModel.Paint(night);
        hull.AddChild(new MeshInstance3D { MaterialOverride = paint, Mesh = hullMesh });
        var turret = new Node3D { Position = Vector3.Up * (VehicleModel.TurretHeight + shape.Deck) };
        hull.AddChild(turret);
        turret.AddChild(new MeshInstance3D { MaterialOverride = paint, Mesh = turretMesh });
        return (hull, turret, VehicleModel.Muzzle(shape), VehicleModel.TurretHeight + shape.Deck + VehicleModel.TurretTop);
    }
    private void SyncTank(Simulation sim, Vehicle tank, int selected)
    {
        // Reinforcements land mid-battle, so a vehicle may be new since the last frame.
        if (!models.TryGetValue(tank.Id, out var model))
        {
            CreateTank(tank);
            model = models[tank.Id];
        }

        model.Hull.Position = World(tank.Position);
        model.Hull.Visible = sim.Recon.IsVisible(tank);
        model.Hull.Rotation = new(0, tank.Heading, 0);
        model.Turret.Rotation = new(0, tank.TurretHeading - tank.Heading, 0);
        model.Selection.Visible = tank.Alive && !tank.Enemy && tank.Group == selected;
        if (!tank.Alive && model.Marker.Visible)
        {
            MakeWreck(tank, model, sim.Elapsed);
        }

        SyncDamage(sim, tank, model);
    }

    private void SyncShells(Simulation sim)
    {
        foreach (int id in shells.Keys.Where(id => sim.Shells.All(s => s.Id != id)).ToArray())
        {
            shells[id].QueueFree();
            shells.Remove(id);
        }

        foreach (var shell in sim.Shells)
        {
            if (!shells.TryGetValue(shell.Id, out var node))
            {
                node = new MeshInstance3D { MaterialOverride = shellMaterial, Mesh = shellMesh };
                AddChild(node);
                shells[shell.Id] = node;
            }

            node.Position = World(shell.Position, ShellHeight);
            node.Rotation = new(0, Geometry.Angle(shell.Direction), 0);
            node.Visible = sim.Recon.PointVisible(shell.Position);
        }
    }

    private void DrawRoutes(Simulation sim, int selected)
    {
        routeLines.ClearSurfaces();
        bool any = false;
        foreach (var tank in sim.Group(selected).Where(t => t.Route.Count > 0))
        {
            if (!any)
            {
                routeLines.SurfaceBegin(Godot.Mesh.PrimitiveType.Lines);
                any = true;
            }

            var previous = tank.Position;
            foreach (var p in tank.Route)
            {
                routeLines.SurfaceAddVertex(World(previous, RouteHeight));
                routeLines.SurfaceAddVertex(World(p, RouteHeight));
                previous = p;
            }
        }
        if (any)
        {
            routeLines.SurfaceEnd();
        }
    }

    private void DrawFog(Reconnaissance recon)
    {
        fogTexture = GridImage.Show(fogTexture, GridImage.Fog(recon, Palette.FogUnexplored, Palette.FogExplored));
        fogMaterial.AlbedoTexture = fogTexture;
    }
    private sealed record Model(Node3D Hull, Node3D Turret, MeshInstance3D Selection, MeshInstance3D Marker, Vector3 Muzzle);
}
