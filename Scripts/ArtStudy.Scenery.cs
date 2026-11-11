namespace Breakwater;

public partial class ArtStudy
{
    private const string RockAsset = "res://assets/art-study/rock_07/rock_07_1k.gltf";
    private const string LowRockAsset = "res://assets/art-study/rock_09/rock_09_1k.gltf";
    private const string TreeAsset = "res://assets/art-study/island_tree_01/island_tree_01_1k.gltf";
    private const string ShrubAsset = "res://assets/art-study/wild_rooibos_bush/wild_rooibos_bush_1k.gltf";
    private const int ScenerySeed = 431, PalmTargetCount = 120;
    private readonly IslandAssets artAssets = new();
    private Material? studyRock;

    private void BuildScenery()
    {
        var rock = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtRock.gdshader") };
        rock.SetShaderParameter("rock_tex", SurfaceTextures.Albedo(SurfaceTextures.Rock));
        rock.SetShaderParameter("stone_color", Palette.Art.Rock);
        rock.SetShaderParameter("moss_color", Palette.Art.Grass);
        studyRock = rock;
        BuildRockFormations();
        foreach (var (p, width, height, rotation) in (ValueTuple<Vector2, float, float, float>[])
        [
            (new(-26, 14), 15, 8, 32), (new(22, -14), 12, 7, 110),
            (new(16, 26), 13, 7, 242), (new(-32, -19), 14, 9, 80),
            (new(-4, -27), 16, 9, 167), (new(30, 10), 12, 6, 32),
        ])
        {
            PlaceAsset(TreeAsset, p, width, height, rotation, -.2f);
        }

        var random = new Random(ScenerySeed);
        BuildRockyCoast();

        var palms = Enumerable.Range(0, 3).Select(ArtStudyPalm.Build).ToArray();
        int placed = 0;
        for (int attempt = 0; attempt < 2500 && placed < PalmTargetCount; attempt++)
        {
            var p = new Vector2(random.NextSingle() * 162 - 81, random.NextSingle() * 124 - 62);
            float shore = Shore(p);
            if (shore < 7 || RoadDistance(p) < 10 || p.Length() < 22 || p.DistanceTo(new(14, -50)) < 17 || (p.X < -54 && p.Y > 0 && p.Y < 26))
            {
                continue;
            }
            float clusters = landscapeNoise.GetNoise2D(p.X * .85f + 100, p.Y * .85f) + .5f;
            if (clusters < .42f)
            {
                continue;
            }
            AddChild(new MeshInstance3D
            {
                Mesh = palms[placed % palms.Length],
                Position = Ground(p),
                Rotation = new(0, random.NextSingle() * Mathf.Tau, 0),
                Scale = Vector3.One * (.8f + random.NextSingle() * .65f),
            });
            if (placed % 5 == 0)
            {
                PlaceAsset(ShrubAsset, p + new Vector2(3, -2), 4 + random.NextSingle() * 2, 2.7f, random.NextSingle() * 360, -.15f);
            }
            placed++;
        }
        // Low, leafy growth joins the outcrops to the soil and gives the palms an understorey.
        for (int i = 0; i < 24; i++)
        {
            float angle = i * 2.39996f;
            float radius = 12 + random.NextSingle() * 26;
            var p = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .85f);
            if (RoadDistance(p) > 6)
            {
                PlaceAsset(TreeAsset, p, 5 + random.NextSingle() * 5, 3 + random.NextSingle() * 3, random.NextSingle() * 360, -.4f);
            }
        }
        BuildCliffDetail();
        BuildDetailedHarbour();
        BuildHarbourWeathering();
        BuildGroundCover();
        BuildStation();
        BatchStudyRocks();
        GD.Print($"ART STUDY SCENERY: {placed} palms, shared CC0 rock/shrub meshes with imported LODs.");
    }

    private Node3D PlaceAsset(string path, Vector2 p, float width, float height, float yawDegrees, float bury)
    {
        var root = artAssets.Place(this, path, Ground(p, bury), new(width, height, width), yawDegrees);
        if (path == RockAsset || path == LowRockAsset || path == CliffAsset || path == FaceAsset || path == BoulderAsset)
        {
            rockInstances.Add(root);
        }
        if (path == RockAsset || path == LowRockAsset)
        {
            foreach (var mesh in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                mesh.MaterialOverride = studyRock;
            }
        }
        return root;
    }
    private static StandardMaterial3D Finish(Color color, float roughness = .85f) => new()
    {
        AlbedoColor = color,
        Roughness = roughness,
        MetallicSpecular = .2f,
    };

    private static MeshInstance3D Block(Node parent, Vector3 position, Vector3 size, Material material)
    {
        var mesh = new MeshInstance3D { Position = position, Mesh = new BoxMesh { Size = size }, MaterialOverride = material };
        parent.AddChild(mesh);
        return mesh;
    }

    private static void Pole(Node parent, Vector3 from, Vector3 to, float radius, Material material)
    {
        var direction = to - from;
        var mesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = direction.Length(), RadialSegments = 8 },
            MaterialOverride = material,
            Position = (from + to) / 2,
        };
        parent.AddChild(mesh);
        mesh.Quaternion = new Quaternion(Vector3.Up, direction.Normalized());
    }


    private void BuildStation()
    {
        var station = new Node3D { Position = Ground(new(10, -52), .05f) };
        AddChild(station);
        var concrete = SurfaceTextures.Material(SurfaceTextures.Concrete, 6, Palette.Art.Concrete);
        var wall = SurfaceTextures.Material(SurfaceTextures.Stucco, 5, Palette.Art.Wall);
        var metal = Finish(Palette.Art.Metal);
        var roof = Finish(Palette.Art.Roof);
        var glass = Finish(Palette.Art.Glass, .2f);
        Block(station, new(0, .08f, 0), new(25, .16f, 16), concrete);
        Block(station, new(0, 2.3f, -1), new(12, 4.4f, 7), wall);
        Block(station, new(0, 4.65f, -1), new(12.5f, .35f, 7.5f), roof);
        Block(station, new(0, .35f, -1), new(12.3f, .5f, 7.3f), concrete);
        Block(station, new(3.7f, 1.55f, 2.53f), new(1.4f, 2.5f, .12f), metal);
        Block(station, new(3.7f, 2.95f, 3), new(2.5f, .15f, 1.8f), roof);
        for (int i = 0; i < 3; i++)
        {
            float x = -4.3f + i * 2.6f;
            Block(station, new(x, 2.4f, 2.54f), new(1.65f, 1.45f, .1f), metal);
            Block(station, new(x, 2.4f, 2.61f), new(1.4f, 1.2f, .06f), glass);
            Block(station, new(x, 2.4f, 2.66f), new(.07f, 1.2f, .08f), roof);
            Block(station, new(x, 1.64f, 2.7f), new(1.85f, .12f, .4f), concrete);
        }
        Block(station, new(-3, 5.1f, -2), new(2.2f, .65f, 1.4f), metal);
        var mastBase = new Vector3(9, .2f, -2);
        Vector3[] legs = [new(-1.2f, 0, -1), new(1.2f, 0, -1), new(0, 0, 1.2f)];
        foreach (var leg in legs)
        {
            Pole(station, mastBase + leg, mastBase + leg * .25f + Vector3.Up * 20, .09f, metal);
        }
        for (int level = 0; level < 7; level++)
        {
            float bottom = level * 2.8f;
            float scale = 1 - bottom / 26;
            for (int side = 0; side < 3; side++)
            {
                Pole(station, mastBase + legs[side] * scale + Vector3.Up * bottom, mastBase + legs[(side + 1) % 3] * (scale - .1f) + Vector3.Up * (bottom + 2.8f), .045f, metal);
                Pole(station, mastBase + legs[side] * scale + Vector3.Up * bottom, mastBase + legs[(side + 1) % 3] * scale + Vector3.Up * bottom, .05f, metal);
            }
        }
        Pole(station, mastBase + Vector3.Up * 19, mastBase + Vector3.Up * 24, .045f, metal);
        for (int dish = 0; dish < 3; dish++)
        {
            var bowl = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 24, Rings = 12 },
                MaterialOverride = Finish(Palette.Art.Wall),
                Position = mastBase + new Vector3(-.8f, 9 + dish * 4, 1.5f),
                Scale = new(1, 1, .22f),
                RotationDegrees = new(-15, -25 + dish * 35, 0),
            };
            station.AddChild(bowl);
            Pole(station, bowl.Position, bowl.Position + new Vector3(0, .1f, 1), .055f, metal);
        }
        for (int x = -12; x <= 12; x += 3)
        {
            Pole(station, new(x, .1f, -7), new(x, 2, -7), .06f, metal);
        }
        Pole(station, new(-12, 1, -7), new(12, 1, -7), .025f, metal);
        Pole(station, new(-12, 1.8f, -7), new(12, 1.8f, -7), .025f, metal);
    }
}
