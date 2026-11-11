namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>The art-study coast and materials fitted around the simulation's flat, playable square.</summary>
public partial class BattleView
{
    private const float IslandExtent = 180, IslandStep = 2;
    private const int IslandFieldSize = 512;
    private readonly IslandAssets islandAssets = new(SeaLevel);
    private readonly FastNoiseLite coastNoise = new() { Seed = 817, Frequency = .055f };
    // Assigned by Build before any scenery construction.
    private Simulation scenerySim = null!;
    private ArrayMesh[] islandPalms = [];

    private float IslandShore(Vector2 p)
    {
        // Rounded, irregular shoreline outside every navigable point, including the map corners.
        var q = p.Abs() - Vector2.One * 88;
        float roundedBox = new Vector2(Math.Max(q.X, 0), Math.Max(q.Y, 0)).Length() + Math.Min(Math.Max(q.X, q.Y), 0) - 24;
        return -roundedBox + coastNoise.GetNoise2D(p.X, p.Y) * 3;
    }

    private float IslandHeight(Vector2 p) => Math.Min(0, Math.Max(-12, SeaLevel + IslandShore(p) * .14f));

    private static float ModernRoadDistance(Vector2 p, int island) => Math.Min(
        Geometry.DistanceToSegment(new(p.X, p.Y), new(0, -89), new(0, 93)) - 1,
        Geometry.DistanceToSegment(new(p.X, p.Y), new(-92.5f, RoadNorthing[island]), new(64.5f, RoadNorthing[island])));

    private void BuildIsland(int island, bool modernRoads)
    {
        coastNoise.Seed = 817 + island * 31;
        var field = Image.CreateEmpty(IslandFieldSize, IslandFieldSize, false, Image.Format.Rgbh);
        for (int y = 0; y < IslandFieldSize; y++)
        {
            for (int x = 0; x < IslandFieldSize; x++)
            {
                var p = new Vector2(x, y) / (IslandFieldSize - 1) * (IslandExtent * 2) - Vector2.One * IslandExtent;
                float road = modernRoads ? ModernRoadDistance(p, island) : 24;
                field.SetPixel(x, y, new(Math.Clamp(.5f + IslandShore(p) / 80, 0, 1), Math.Clamp(road / 24, 0, 1), .5f + coastNoise.GetNoise2D(p.X, p.Y) * .5f));
            }
        }
        field.GenerateMipmaps();
        var fieldTexture = ImageTexture.CreateFromImage(field);
        var ground = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtGround.gdshader") };
        ground.SetShaderParameter("land_field", fieldTexture);
        ground.SetShaderParameter("extent", IslandExtent);
        ground.SetShaderParameter("road_half_width", 3.5f);
        foreach (var (name, texture) in (ValueTuple<string, string>[])
        [
            ("sand", SurfaceTextures.Sand), ("grass", SurfaceTextures.Grass),
            ("soil", SurfaceTextures.ForestFloor), ("asphalt", SurfaceTextures.Asphalt), ("rock", SurfaceTextures.Rock),
        ])
        {
            ground.SetShaderParameter(name + "_tex", SurfaceTextures.Albedo(texture));
        }
        ground.SetShaderParameter("sand_normal", SurfaceTextures.Normal(SurfaceTextures.Sand));
        ground.SetShaderParameter("grass_normal", SurfaceTextures.Normal(SurfaceTextures.Grass));
        ground.SetShaderParameter("sand_color", Palette.Art.Sand);
        ground.SetShaderParameter("grass_color", Palette.Art.Grass);
        ground.SetShaderParameter("earth_color", Palette.Art.Earth);
        ground.SetShaderParameter("road_color", Palette.Art.Road);
        ground.SetShaderParameter("rock_color", Palette.Art.Rock);
        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);
        int cells = (int)(IslandExtent * 2 / IslandStep);
        Vector2I[] corners = [new(0, 0), new(1, 0), new(1, 1), new(0, 0), new(1, 1), new(0, 1)];
        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                foreach (var offset in corners)
                {
                    var p = new Vector2(x + offset.X, z + offset.Y) * IslandStep - Vector2.One * IslandExtent;
                    float dx = IslandHeight(p + Vector2.Right) - IslandHeight(p - Vector2.Right);
                    float dz = IslandHeight(p + Vector2.Down) - IslandHeight(p - Vector2.Down);
                    tool.SetNormal(new Vector3(-dx, 2, -dz).Normalized());
                    tool.SetUV((p + Vector2.One * IslandExtent) / (IslandExtent * 2));
                    tool.AddVertex(new(p.X, IslandHeight(p), p.Y));
                }
            }
        }
        tool.GenerateTangents();
        tool.Index();
        AddChild(new MeshInstance3D { Name = "IslandGround", Mesh = tool.Commit(), MaterialOverride = ground, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var water = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtWater.gdshader") };
        water.SetShaderParameter("land_field", fieldTexture);
        water.SetShaderParameter("extent", IslandExtent);
        water.SetShaderParameter("deep_color", Palette.Art.Sea);
        water.SetShaderParameter("shallow_color", Palette.Art.Lagoon);
        water.SetShaderParameter("foam_color", Palette.Art.Foam);
        AddChild(new MeshInstance3D
        {
            Name = "Lagoon", MaterialOverride = water,
            Mesh = new PlaneMesh { Size = new(1800, 1800), SubdivideDepth = 180, SubdivideWidth = 180 },
            Position = new(0, SeaLevel, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        islandPalms = Enumerable.Range(0, 3).Select(ArtStudyPalm.Build).ToArray();
        BuildIslandCoast(island);
        // The logistics quay and cargo stay beyond the navigable boundary.
        IslandHarbour.Build(this, new(-112, 0, 30 - island * 34), SeaLevel);
        var cargo = new ArtStudyBatch();
        var timber = SurfaceTextures.Material(SurfaceTextures.Bark, 3, Palette.Art.Timber);
        var metal = IslandHarbour.HarbourMaterial(Palette.Art.Metal, .5f, SeaLevel);
        for (int i = 0; i < 8; i++)
        {
            IslandHarbour.CargoCrate(cargo, new(-78 + i * 3, 0, 96 + i % 2 * 2), timber, metal);
            var p = new Vector3(66 + i % 4 * 1.4f, 0, 96 + i / 4 * 1.4f);
            cargo.Beam(p, p + Vector3.Up * 1.3f, .42f, metal, 16);
        }
        cargo.Commit(this);
    }

    private void BuildIslandCoast(int island)
    {
        var random = new Random(52 + island);
        for (int i = 0; i < 84; i++)
        {
            int side = i % 4;
            float along = random.NextSingle() * 192 - 96;
            float across = 96 + random.NextSingle() * 6;
            var p = side switch { 0 => new Vector2(across, along), 1 => new(-across, along), 2 => new(along, across), _ => new(along, -across) };
            if (p.X < -92 && Math.Abs(p.Y - (30 - island * 34)) < 15)
            {
                continue;
            }
            float scale = .7f + random.NextSingle() * .45f;
            AddChild(new MeshInstance3D
            {
                Mesh = islandPalms[i % 3], Position = new(p.X, IslandHeight(p), p.Y),
                Rotation = new(0, random.NextSingle() * Mathf.Tau, 0), Scale = Vector3.One * scale,
            });
            if (i % 3 == 0)
            {
                var rock = p * 1.065f;
                islandAssets.Place(this, IslandAssets.Boulder, new(rock.X, IslandHeight(rock) - .6f, rock.Y), new(4 + random.NextSingle() * 4, 2.3f, 5), random.NextSingle() * 360);
            }
        }
    }

    private bool ClearScenery(V2 p, float margin) => scenerySim.Obstacles.All(o =>
        Math.Abs(p.X - o.Center.X) > o.HalfSize.X + margin || Math.Abs(p.Y - o.Center.Y) > o.HalfSize.Y + margin);

    private void BuildIslandCover(Simulation sim)
    {
        var random = new Random(927 + sim.Campaign.Island);
        var grass = IslandGroundCover.BuildGrassTuft();
        var cells = new Dictionary<Vector2I, List<Transform3D>>();
        for (int i = 0; i < 10000; i++)
        {
            var p = new V2(random.NextSingle() * 184 - 92, random.NextSingle() * 184 - 92);
            var terrain = sim.Terrain?.At(p) ?? Terrain.Open;
            if (terrain is Terrain.Road or Terrain.Track or Terrain.JungleTrack or Terrain.River ||
                (sim.Terrain == null && ModernRoadDistance(new(p.X, p.Y), sim.Campaign.Island) < 5.4f) ||
                !ClearScenery(p, .7f) || sim.Sites.Any(s => V2.Distance(p, s.Position) < 9) ||
                Math.Abs(p.Y - Mission.SupplyLineY) < 1.4f)
            {
                continue;
            }
            float density = coastNoise.GetNoise2D(p.X * 2, p.Y * 2) + .32f;
            if (random.NextSingle() > density)
            {
                continue;
            }
            var cell = new Vector2I(Mathf.FloorToInt(p.X / 24), Mathf.FloorToInt(p.Y / 24));
            if (!cells.TryGetValue(cell, out var transforms))
            {
                transforms = [];
                cells.Add(cell, transforms);
            }
            float size = .45f + random.NextSingle() * .65f;
            // Open ground remains low enough to see routes, hulls and capture rings.
            transforms.Add(new(new Basis(Vector3.Up, random.NextSingle() * Mathf.Tau).Scaled(new(size, size * .6f, size)), World(p, .025f)));
        }
        foreach (var transforms in cells.Values)
        {
            var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = grass, InstanceCount = transforms.Count };
            for (int i = 0; i < transforms.Count; i++)
            {
                multimesh.SetInstanceTransform(i, transforms[i]);
            }
            AddChild(new MultiMeshInstance3D { Multimesh = multimesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, VisibilityRangeEnd = 300, VisibilityRangeEndMargin = 20 });
        }
        GD.Print($"ISLAND SCENERY: {cells.Values.Sum(c => c.Count)} low grass tufts in {cells.Count} batches; island {sim.Campaign.Island + 1}.");
    }
}
