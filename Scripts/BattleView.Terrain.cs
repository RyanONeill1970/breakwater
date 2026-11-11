namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>
/// Classic roads, tracks and jungle follow the simulation grid. Scanned foliage is placed in
/// jungle cells, with open routes and objectives kept clear.
/// </summary>
public partial class BattleView
{
    /// <summary>Ground cells sit just above the island surface and below the fog overlay.</summary>
    private const float TerrainHeight = .02f;

    // The grid planes are see-through, so they draw first among the transparent things, ground before fog,
    // leaving smoke, rings and effects to draw over them.
    private const int GroundPriority = -2, FogPriority = -1;

    private const string GroundShaderPath = "res://Shaders/Ground.gdshader";

    /// <summary>Corner order for a cell's two triangles.</summary>
    private static readonly int[] QuadCorners = [0, 1, 2, 0, 2, 3];

    private void BuildTerrain(TerrainMap terrain)
    {
        var material = new ShaderMaterial { RenderPriority = GroundPriority, Shader = GD.Load<Shader>(GroundShaderPath) };
        material.SetShaderParameter("terrain", ImageTexture.CreateFromImage(GridImage.Ground(terrain, Palette.Art.Ground)));
        material.SetShaderParameter("detail", SurfaceTextures.Albedo(SurfaceTextures.ForestFloor));
        material.SetShaderParameter("detail_normal", SurfaceTextures.Normal(SurfaceTextures.ForestFloor));
        var mean = SurfaceTextures.Mean(SurfaceTextures.ForestFloor);
        material.SetShaderParameter("detail_mean", new Vector3(mean.R, mean.G, mean.B));
        AddChild(GridPlane(TerrainHeight, material));
        BuildTrees(terrain);
    }

    /// <summary>A see-through material for a <see cref="GridPlane"/>, smoothly filtered.</summary>
    private static StandardMaterial3D GridMaterial(bool unshaded, int priority) => new()
    {
        RenderPriority = priority,
        ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };

    /// <summary>A flat square over the exploration grid at <paramref name="height"/>, its texture one cell per <see cref="GridImage"/> block.</summary>
    private static MeshInstance3D GridPlane(float height, Material material)
    {
        float min = Reconnaissance.GridOrigin - Reconnaissance.CellSize / 2;
        float max = min + Reconnaissance.GridSize * Reconnaissance.CellSize;
        var plane = new SurfaceTool();
        plane.Begin(Godot.Mesh.PrimitiveType.Triangles);
        Vector3[] corners = [new(min, height, min), new(max, height, min), new(max, height, max), new(min, height, max)];
        Vector2[] uvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        foreach (int corner in QuadCorners)
        {
            plane.SetNormal(Vector3.Up);
            plane.SetUV(uvs[corner]);
            plane.AddVertex(corners[corner]);
        }

        return new MeshInstance3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = material,
            Mesh = plane.Commit(),
        };
    }
    private void BuildTrees(TerrainMap terrain)
    {
        var random = new Random(71);
        for (int i = 0; i < TerrainMap.GridSize * TerrainMap.GridSize; i++)
        {
            float chance = terrain[i] switch
            {
                Terrain.JungleEdge => .18f,
                Terrain.JungleFringe => .3f,
                Terrain.DeepJungle => .45f,
                _ => 0,
            };
            if (random.NextSingle() >= chance)
            {
                continue;
            }
            var spot = Reconnaissance.CellCenter(i) + new V2(random.NextSingle() - .5f, random.NextSingle() - .5f) * 2;
            if (!ClearScenery(spot, 2) || scenerySim.Sites.Any(s => V2.Distance(spot, s.Position) < 10) || Math.Abs(spot.X) > Mission.Edge || Math.Abs(spot.Y) > Mission.Edge)
            {
                continue;
            }
            float width = 5 + random.NextSingle() * 3;
            float height = 5 + random.NextSingle() * 4;
            if (i % 4 == 0)
            {
                AddChild(new MeshInstance3D
                {
                    Mesh = islandPalms[i % 3], Position = World(spot),
                    Rotation = new(0, random.NextSingle() * Mathf.Tau, 0), Scale = Vector3.One * (height / 10),
                });
            }
            else
            {
                islandAssets.Place(this, IslandAssets.Tree, World(spot), new(width, height, width), random.NextSingle() * 360);
            }
            if (i % 3 == 0)
            {
                islandAssets.Place(this, IslandAssets.Fern, World(spot), new(2.5f, 1.2f, 2.5f), random.NextSingle() * 360);
            }
        }
    }
}
