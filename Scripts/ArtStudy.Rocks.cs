namespace Breakwater;

public partial class ArtStudy
{
    private const int RockSeed = 1771;
    private const float RockCellSize = 28;
    private readonly List<Node3D> rockInstances = [];

    private void BuildRockFormations()
    {
        // Interlocking, differently sized outcrops make a stepped crest instead of six inflated stones.
        foreach (var (p, width, height, yawAngle, bury) in (ValueTuple<Vector2, float, float, float, float>[])
        [
            (new(-7, -6), 19, 15, -32, -4), (new(6, -10), 17, 12, 63, -3),
            (new(-15, 5), 18, 15, 127, -4), (new(3, 8), 18, 13, 210, -5),
            (new(13, 10), 14, 11, 290, -3), (new(-7, 20), 14, 12, 44, -3),
            (new(9, 23), 13, 9, 162, -3), (new(-17, -15), 13, 12, 112, -3),
            (new(15, -20), 12, 9, 238, -3), (new(-21, 15), 12, 13, 330, -3),
        ])
        {
            PlaceAsset(BoulderAsset, p, width, height, yawAngle, bury);
        }
        var random = new Random(RockSeed);
        // A broken skirt of smaller rocks connects the crest to the terrain and catches the light.
        for (int i = 0; i < 48; i++)
        {
            float angle = i * 2.39996f;
            float radius = 17 + random.NextSingle() * 15;
            var p = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .86f);
            if (RoadDistance(p) < 7 || (p.X < -26 && p.Y > -22 && p.Y < 24))
            {
                continue;
            }
            float width = 3 + random.NextSingle() * 6;
            PlaceAsset(BoulderAsset, p, width, width * (.7f + random.NextSingle() * .55f), random.NextSingle() * 360, -1.4f);
        }
    }

    private void BuildRockyCoast()
    {
        var random = new Random(RockSeed + 1);
        for (int group = 0; group < 19; group++)
        {
            float angle = group * Mathf.Tau / 19;
            var p = new Vector2(Mathf.Cos(angle) * 95, Mathf.Sin(angle) * 73);
            if ((p.X < -66 && Math.Abs(p.Y - 11) < 29) || (p.X > -8 && p.X < 28 && p.Y < -45))
            {
                continue;
            }
            // Three large fragments, then smaller tidal rubble. Leave sand coves between groups.
            for (int fragment = 0; fragment < 8; fragment++)
            {
                var at = p + new Vector2(random.NextSingle() * 18 - 9, random.NextSingle() * 13 - 6.5f);
                float width = fragment < 3 ? 5 + random.NextSingle() * 6 : 1.4f + random.NextSingle() * 3.2f;
                if (RoadDistance(at) < width * .6f + 4)
                {
                    continue;
                }
                float height = width * (.6f + random.NextSingle() * .5f);
                var rock = PlaceAsset(BoulderAsset, at, width, height, random.NextSingle() * 360, -height * .3f);
                // Partly submerged pieces make the surf break around a real uneven silhouette.
                if (Shore(at) < 0)
                {
                    rock.Position = rock.Position with { Y = -height * (.45f + random.NextSingle() * .25f) };
                }
            }
        }
    }

    private void BatchStudyRocks()
    {
        var batches = new Dictionary<(Mesh Mesh, Material Material, Vector2I Cell), List<Transform3D>>();
        foreach (var root in rockInstances)
        {
            foreach (var instance in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                // These scans have one surface. Keep any future multi-surface import intact.
                if (instance.Mesh.GetSurfaceCount() != 1 || instance.GetActiveMaterial(0) is not { } material)
                {
                    continue;
                }
                var origin = instance.GlobalPosition;
                var cell = new Vector2I(Mathf.FloorToInt(origin.X / RockCellSize), Mathf.FloorToInt(origin.Z / RockCellSize));
                var key = (instance.Mesh, material, cell);
                if (!batches.TryGetValue(key, out var transforms))
                {
                    transforms = [];
                    batches.Add(key, transforms);
                }
                transforms.Add(GlobalTransform.AffineInverse() * instance.GlobalTransform);
                instance.QueueFree();
            }
        }
        foreach (var ((mesh, material, _), transforms) in batches)
        {
            AddSceneryBatch(mesh, transforms, material);
        }
        GD.Print($"ART ROCKS: {rockInstances.Count} fragments in {batches.Count} spatial batches.");
    }

    private void AddSceneryBatch(Mesh mesh, List<Transform3D> transforms, Material? material = null)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = transforms.Count,
        };
        for (int i = 0; i < transforms.Count; i++)
        {
            multimesh.SetInstanceTransform(i, transforms[i]);
        }
        AddChild(new MultiMeshInstance3D { Multimesh = multimesh, MaterialOverride = material, LodBias = .35f });
    }
}
