namespace Breakwater;

public partial class ArtStudy
{
    private const string CliffAsset = "res://assets/art-study/coastal_cliff_01/coastal_cliff_01_2k.gltf";
    private const string FaceAsset = "res://assets/art-study/rock_face_02/rock_face_02_2k.gltf";
    private const string BoulderAsset = "res://assets/art-study/boulder_01/boulder_01_2k.gltf";
    private const string FernAsset = "res://assets/art-study/fern_02/fern_02_2k.gltf";
    private const int DetailSeed = 927, CoverAttempts = 15000;
    private const float CoverCellSize = 18;

    private void BuildCliffDetail()
    {
        // The long scan is kept low and layered into the hillside; smaller complete faces close its ends.
        var cliff = PlaceAsset(CliffAsset, new(-32, 2), 46, 14, -88, -3);
        cliff.Position = cliff.Position with { Y = BaseGroundHeight(new(-32, 2)) - 3 };
        PlaceAsset(FaceAsset, new(-35, 19), 10, 9, -48, -3);
        PlaceAsset(FaceAsset, new(-28, 25), 10, 8, -8, -3);
        PlaceAsset(FaceAsset, new(-27, -22), 11, 8, -140, -3);
        PlaceAsset(BoulderAsset, new(-31, -15), 10, 8, 65, -2);
        PlaceAsset(BoulderAsset, new(-38, 7), 9, 8, 125, -2);
        PlaceAsset(BoulderAsset, new(-28, 22), 12, 12, 25, -2);
        var random = new Random(DetailSeed);
        for (int i = 0; i < 38; i++)
        {
            float z = -27 + random.NextSingle() * 57;
            var p = new Vector2(-41 + random.NextSingle() * 9, z);
            if (RoadDistance(p) < 5)
            {
                continue;
            }
            float size = 1 + random.NextSingle() * 4;
            PlaceAsset(BoulderAsset, p, size, size * (.45f + random.NextSingle() * .3f), random.NextSingle() * 360, -.5f);
        }
        // A sheltered rocky lip at the inlet and rubble around the road retaining wall.
        foreach (var (p, width, height, yawAngle) in (ValueTuple<Vector2, float, float, float>[])
        [
            (new(-79, -18), 12, 4, 85), (new(-84, -23), 11, 3.5f, 130),
            (new(-75, 29), 9, 5, 50), (new(-73, 36), 11, 6, 100),
        ])
        {
            PlaceAsset(BoulderAsset, p, width, height, yawAngle, -1.6f);
        }
    }

    private void BuildGroundCover()
    {
        var random = new Random(DetailSeed);
        var grass = IslandGroundCover.BuildGrassTuft();
        var fernScene = GD.Load<PackedScene>(FernAsset).Instantiate<Node3D>();
        var ferns = fernScene.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Select(n => n.Mesh).ToArray();
        var fernMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtLeaves.gdshader") };
        fernMaterial.SetShaderParameter("leaf_tex", GD.Load<Texture2D>("res://assets/art-study/fern_02/textures/fern_02_diff_2k.jpg"));
        fernMaterial.SetShaderParameter("leaf_color", Palette.Art.Leaf);
        var cells = new Dictionary<(Vector2I Cell, int Kind), List<Transform3D>>();
        int grassCount = 0, fernCount = 0;
        for (int i = 0; i < CoverAttempts; i++)
        {
            var p = new Vector2(-83 + random.NextSingle() * 65, -32 + random.NextSingle() * 82);
            float roadDistance = RoadDistance(p);
            // Keep hard surfaces clear, and keep the tall plants away from the road edge.
            if (Shore(p) < 5 || roadDistance < 4.3f || (p.X < -66 && p.Y > -.5f && p.Y < 22.5f) || (p.X > -44 && p.Y > -24 && p.Y < 25))
            {
                continue;
            }
            float density = landscapeNoise.GetNoise2D(p.X * 2.3f, p.Y * 2.3f) + .46f;
            if (random.NextSingle() > density * .9f)
            {
                continue;
            }
            bool fern = i % 13 == 0 && roadDistance > 6 && ferns.Length > 0;
            int kind = fern ? 1 + i % ferns.Length : 0;
            var mesh = fern ? ferns[kind - 1] : grass;
            float size = fern ? 2.2f + random.NextSingle() * 1.4f : .45f + random.NextSingle() * .7f;
            var basis = new Basis(Vector3.Up, random.NextSingle() * Mathf.Tau).Scaled(new(size, size * (.7f + random.NextSingle() * .6f), size));
            var bounds = mesh.GetAabb();
            var origin = Ground(p, -.02f) - basis * new Vector3(bounds.GetCenter().X, bounds.Position.Y, bounds.GetCenter().Z);
            var cell = new Vector2I(Mathf.FloorToInt(p.X / CoverCellSize), Mathf.FloorToInt(p.Y / CoverCellSize));
            var key = (cell, kind);
            if (!cells.TryGetValue(key, out var transforms))
            {
                transforms = [];
                cells.Add(key, transforms);
            }
            transforms.Add(new(basis, origin));
            if (fern)
            {
                fernCount++;
            }
            else
            {
                grassCount++;
            }
        }
        // Fern crowns on the filled ledges join the cliff to the vegetation above it.
        for (int i = 0; i < 230 && ferns.Length > 0; i++)
        {
            var p = new Vector2(-29 + random.NextSingle() * 47, -22 + random.NextSingle() * 46);
            var mesh = ferns[i % ferns.Length];
            float size = 2 + random.NextSingle() * 1.5f;
            var basis = new Basis(Vector3.Up, random.NextSingle() * Mathf.Tau).Scaled(Vector3.One * size);
            var bounds = mesh.GetAabb();
            var origin = Ground(p) - basis * new Vector3(bounds.GetCenter().X, bounds.Position.Y, bounds.GetCenter().Z);
            var cell = new Vector2I(Mathf.FloorToInt(p.X / CoverCellSize), Mathf.FloorToInt(p.Y / CoverCellSize));
            var key = (cell, 1 + i % ferns.Length);
            if (!cells.TryGetValue(key, out var transforms))
            {
                transforms = [];
                cells.Add(key, transforms);
            }
            transforms.Add(new(basis, origin));
            fernCount++;
        }
        foreach (var ((_, kind), transforms) in cells)
        {
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = kind == 0 ? grass : ferns[kind - 1],
                InstanceCount = transforms.Count,
            };
            for (int i = 0; i < transforms.Count; i++)
            {
                multimesh.SetInstanceTransform(i, transforms[i]);
            }
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = multimesh,
                MaterialOverride = kind == 0 ? null : fernMaterial,
                CastShadow = kind == 0 ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
                VisibilityRangeEnd = kind == 0 ? 175 : 220,
                VisibilityRangeEndMargin = 15,
                LodBias = .45f,
            });
        }
        fernScene.Free();
        GD.Print($"HARBOUR COVER: {grassCount} grass tufts, {fernCount} ferns in {cells.Count} spatial batches.");
    }

}
