namespace Breakwater;

public partial class ArtStudy
{
    private const float LandscapeExtent = 140, TerrainStep = 1.0f, ShoreEncoding = 40;
    private const int FieldResolution = 1024;
    private const float RoadWidth = 6.4f;
    private static readonly Vector2[] RoadKnots =
    [
        new(-62, 12), new(-51, 37), new(-24, 49), new(8, 47), new(37, 32),
        new(48, 7), new(36, -23), new(12, -43), new(-17, -43), new(-40, -24), new(-57, -6),
    ];
    private readonly List<Vector2> road = [];
    private readonly FastNoiseLite landscapeNoise = new() { Seed = 817, Frequency = .075f };

    private static float Shore(Vector2 p)
    {
        float angle = Mathf.Atan2(p.Y / 73, p.X / 96);
        float radius = new Vector2(p.X / 96, p.Y / 73).Length();
        float boundary = 1 + .08f * Mathf.Sin(angle * 3 + .6f) + .045f * Mathf.Cos(angle * 7 - .4f) + .018f * Mathf.Sin(angle * 17);
        float coast = (boundary - radius) * 78;
        // A shallow bay on the west gives the pier a sheltered inlet.
        float inlet = p.DistanceTo(new(-91, -7)) - 24;
        return Math.Min(coast, inlet);
    }

    private float GroundHeight(Vector2 p)
    {
        float original = BaseGroundHeight(p);
        if (original < 0)
        {
            return original;
        }
        // Fill the volume behind the harbour cliff scan; exposed scans must never read as thin walls.
        float along = 1 - Mathf.SmoothStep(19, 31, Math.Abs(p.Y + 3));
        float front = Mathf.SmoothStep(-36, -27, p.X);
        float back = 1 - Mathf.SmoothStep(-22, -10, p.X);
        float ridge = (15.8f + landscapeNoise.GetNoise2D(p.X * .55f, p.Y * .55f) * 2) * along * front * back;
        return Math.Max(original, ridge);
    }

    private float BaseGroundHeight(Vector2 p)
    {
        float inland = Shore(p);
        if (inland < 0)
        {
            return Math.Max(-12, inland * .24f);
        }

        float plateau = Mathf.SmoothStep(0, 13, inland);
        float hill = Mathf.Exp(-(p.X * p.X / 600 + p.Y * p.Y / 420)) * 20;
        float ridges = landscapeNoise.GetNoise2D(p.X, p.Y) * hill * .08f;
        return plateau * (2.4f + landscapeNoise.GetNoise2D(p.X, p.Y) * .9f + hill + ridges);
    }

    private Vector3 Ground(Vector2 p, float offset = 0) => new(p.X, GroundHeight(p) + offset, p.Y);

    private float RoadDistance(Vector2 p)
    {
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < road.Count; i++)
        {
            var a = road[i];
            var b = road[(i + 1) % road.Count];
            var segment = b - a;
            float t = Math.Clamp((p - a).Dot(segment) / segment.LengthSquared(), 0, 1);
            nearest = Math.Min(nearest, p.DistanceTo(a + t * segment));
        }
        // A short branch reaches the pier from the ring road.
        var pierA = new Vector2(-80, 12);
        var pierB = new Vector2(-62, 12);
        nearest = Math.Min(nearest, p.DistanceTo(pierA + (pierB - pierA) * Math.Clamp((p - pierA).Dot(pierB - pierA) / pierA.DistanceSquaredTo(pierB), 0, 1)));
        return nearest;
    }

    private void BuildLandscape()
    {
        for (int i = 0; i < RoadKnots.Length; i++)
        {
            var a = RoadKnots[(i - 1 + RoadKnots.Length) % RoadKnots.Length];
            var b = RoadKnots[i];
            var c = RoadKnots[(i + 1) % RoadKnots.Length];
            var d = RoadKnots[(i + 2) % RoadKnots.Length];
            for (int sample = 0; sample < 12; sample++)
            {
                road.Add(b.CubicInterpolate(c, a, d, sample / 12f));
            }
        }

        // Half precision avoids the visible stair-step bands that 8-bit shore distances created in foam.
        var field = Image.CreateEmpty(FieldResolution, FieldResolution, false, Image.Format.Rgbh);
        for (int y = 0; y < FieldResolution; y++)
        {
            for (int x = 0; x < FieldResolution; x++)
            {
                var p = new Vector2(x, y) / (FieldResolution - 1) * (LandscapeExtent * 2) - Vector2.One * LandscapeExtent;
                field.SetPixel(x, y, new Color(Math.Clamp(.5f + Shore(p) / (ShoreEncoding * 2), 0, 1), Math.Clamp(RoadDistance(p) / 24, 0, 1), .5f + landscapeNoise.GetNoise2D(p.X, p.Y) * .5f));
            }
        }
        field.GenerateMipmaps();
        var fieldTexture = ImageTexture.CreateFromImage(field);
        var ground = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtGround.gdshader") };
        ground.SetShaderParameter("land_field", fieldTexture);
        ground.SetShaderParameter("sand_tex", SurfaceTextures.Albedo(SurfaceTextures.Sand));
        ground.SetShaderParameter("grass_tex", SurfaceTextures.Albedo(SurfaceTextures.Grass));
        ground.SetShaderParameter("soil_tex", SurfaceTextures.Albedo(SurfaceTextures.ForestFloor));
        ground.SetShaderParameter("asphalt_tex", SurfaceTextures.Albedo(SurfaceTextures.Asphalt));
        ground.SetShaderParameter("sand_normal", SurfaceTextures.Normal(SurfaceTextures.Sand));
        ground.SetShaderParameter("grass_normal", SurfaceTextures.Normal(SurfaceTextures.Grass));
        ground.SetShaderParameter("sand_color", Palette.Art.Sand);
        ground.SetShaderParameter("grass_color", Palette.Art.Grass);
        ground.SetShaderParameter("earth_color", Palette.Art.Earth);
        ground.SetShaderParameter("road_color", Palette.Art.Road);
        ground.SetShaderParameter("rock_tex", SurfaceTextures.Albedo(SurfaceTextures.Rock));
        ground.SetShaderParameter("rock_color", Palette.Art.Rock);
        ground.SetShaderParameter("extent", LandscapeExtent);
        ground.SetShaderParameter("road_half_width", RoadWidth / 2);

        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        int cells = (int)(LandscapeExtent * 2 / TerrainStep);
        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                foreach (var offset in (Vector2I[])[new(0, 0), new(1, 0), new(1, 1), new(0, 0), new(1, 1), new(0, 1)])
                {
                    var p = new Vector2(x + offset.X, z + offset.Y) * TerrainStep - Vector2.One * LandscapeExtent;
                    float dx = GroundHeight(p + Vector2.Right * .3f) - GroundHeight(p - Vector2.Right * .3f);
                    float dz = GroundHeight(p + Vector2.Down * .3f) - GroundHeight(p - Vector2.Down * .3f);
                    tool.SetNormal(new Vector3(-dx, .6f, -dz).Normalized());
                    tool.SetUV((p + Vector2.One * LandscapeExtent) / (LandscapeExtent * 2));
                    tool.AddVertex(Ground(p));
                }
            }
        }
        tool.GenerateTangents();
        AddChild(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = ground, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        BuildRoadMarkings();

        var water = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtWater.gdshader") };
        water.SetShaderParameter("land_field", fieldTexture);
        water.SetShaderParameter("extent", LandscapeExtent);
        water.SetShaderParameter("deep_color", Palette.Art.Sea);
        water.SetShaderParameter("shallow_color", Palette.Art.Lagoon);
        water.SetShaderParameter("foam_color", Palette.Art.Foam);
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new(1200, 1200), SubdivideDepth = 140, SubdivideWidth = 140 },
            MaterialOverride = water,
            Position = new(0, -.06f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private void BuildRoadMarkings()
    {
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        float travelled = 0;
        for (int i = 0; i < road.Count; i++)
        {
            var a = road[i];
            var b = road[(i + 1) % road.Count];
            var direction = (b - a).Normalized();
            float length = a.DistanceTo(b);
            for (float along = 0; along < length; along += .5f)
            {
                var p = a + direction * along;
                if (p.X > -10 || Mathf.PosMod(travelled + along, 9) > 3)
                {
                    continue;
                }
                var end = a + direction * Math.Min(length, along + .5f);
                var side = new Vector2(-direction.Y, direction.X) * .1f;
                foreach (var at in (Vector2[])[p + side, p - side, end + side, p - side, end - side, end + side])
                {
                    mesh.SetNormal(Vector3.Up);
                    mesh.AddVertex(Ground(at, .035f));
                }
            }
            travelled += length;
        }
        AddChild(new MeshInstance3D { Mesh = mesh.Commit(), MaterialOverride = Finish(Palette.Art.Markings), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
}
