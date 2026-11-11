namespace Breakwater;

/// <summary>
/// Procedural tree and palm meshes. Each is one mesh with two surfaces, bark and leaves, so a whole
/// forest of one shape draws from a single multimesh. Leaves use <c>Shaders/Foliage.gdshader</c>,
/// which reads each vertex's height in the crown from UV2.y. Meshes stand on their origin, in metres.
/// </summary>
public static class Flora
{
    private const string FoliageShaderPath = "res://Shaders/Foliage.gdshader";

    // Mesh detail: sides round a trunk, and segments and rings round a crown lobe.
    private const int TrunkSides = 7, LobeSegments = 10, LobeRings = 6;

    // Canopy surfaces bulge by up to this many metres; crowns and fronds sway this far in the wind.
    private const float CanopyLumps = .28f, CanopySway = .08f, FrondSway = .22f;

    // Palms: fronds per crown, points along each frond, and the frond's length and widest span in metres.
    private const int Fronds = 10, FrondStations = 9;
    private const float FrondLength = 3.4f, FrondWidth = 1.1f;

    /// <summary>How far each frond's edges hang below its midrib, in metres, so it folds like a real leaf.</summary>
    private const float FrondFold = .12f;

    private const int ShapeSeed = 17;

    private static BaseMaterial3D? bark, palmBark;

    /// <summary>Metres per repeat of the bark textures.</summary>
    private const float BarkTile = 1.5f;
    private static ShaderMaterial? canopy, fronds;

    /// <summary>
    /// Tree trunks and branches: photographed bark, not vertex colours, since a forest's multimesh
    /// tints each tree's vertex colours with its canopy colour.
    /// </summary>
    private static BaseMaterial3D Bark => bark ??= SurfaceTextures.Material(SurfaceTextures.Bark, BarkTile, Colors.White);

    /// <summary>Palm trunks: photographed palm bark, whose ridges are the old leaf scars.</summary>
    private static BaseMaterial3D PalmBark => palmBark ??= SurfaceTextures.Material(SurfaceTextures.PalmBark, BarkTile, Colors.White);

    private static ShaderMaterial Canopy => canopy ??= Foliage(false, CanopyLumps, CanopySway);

    private static ShaderMaterial FrondLeaves => fronds ??= Foliage(true, 0, FrondSway);

    public static ArrayMesh Tree(TreeShape shape)
    {
        var random = new Random(ShapeSeed + (int)shape);
        var trunk = Begin();
        var leaves = Begin();
        switch (shape)
        {
            case TreeShape.Broadleaf:
                Tube(trunk, [Vector3.Zero, new(.1f, 1.2f, 0), new(.05f, 2.6f, .1f)], [.24f, .18f, .13f], _ => Palette.World.Bark);
                Crown(leaves, random, new(.05f, 3.3f, .1f), 1.5f, 1.1f, 4, .95f, 2.1f, 4.5f);
                break;
            case TreeShape.Emergent:
                Tube(trunk, [Vector3.Zero, new(0, 2.5f, .1f), new(.15f, 5.6f, 0)], [.34f, .24f, .17f], _ => Palette.World.Bark);
                foreach (float angle in (float[])[0, 2.1f, 4.2f])
                {
                    var tip = new Vector3(Mathf.Cos(angle) * 1.3f, 6.2f, Mathf.Sin(angle) * 1.3f);
                    Tube(trunk, [new(.15f, 4.8f, 0), tip], [.12f, .06f], _ => Palette.World.Bark);
                }

                Crown(leaves, random, new(.15f, 6.3f, 0), 1.9f, .85f, 5, 1.45f, 5.4f, 7.3f);
                break;
            default:
                Crown(leaves, random, new(0, .9f, 0), 1f, .8f, 3, .7f, 0, 1.9f);
                break;
        }

        // Undergrowth shows no trunk.
        return Finish(shape == TreeShape.Shrub ? null : trunk, Bark, leaves, Canopy);
    }

    /// <summary>A coconut palm, <paramref name="height"/> metres tall, its trunk curving off to +X by <paramref name="lean"/> metres.</summary>
    public static ArrayMesh Palm(float height, float lean, int seed)
    {
        var random = new Random(ShapeSeed + seed);
        var trunk = Begin();
        var leaves = Begin();
        const int stations = 9;
        List<Vector3> path = [];
        List<float> radii = [];
        for (int i = 0; i <= stations; i++)
        {
            float t = (float)i / stations;
            path.Add(new(lean * t * t, height * t, 0));
            radii.Add(Mathf.Lerp(.24f, .15f, t));
        }

        // The old leaf scars show as rings of darker bark.
        Tube(trunk, path, radii, i => i % 2 == 0 ? Palette.World.PalmTrunk : Palette.World.PalmTrunk.Darkened(.22f));
        var top = path[^1];
        for (int f = 0; f < Fronds; f++)
        {
            float azimuth = f * Mathf.Tau / Fronds + random.NextSingle() * .4f;
            // The lower fronds droop further, and are older and yellower.
            float droop = .6f + random.NextSingle() * .8f;
            var color = Palette.World.PalmLeaf.Lerp(Palette.World.PalmLeafOld, random.NextSingle() * random.NextSingle());
            Frond(leaves, top, azimuth, droop, color);
        }

        // Coconuts share the leaf surface, drawn on the frond's midrib where no leaflet is cut away.
        for (int n = 0; n < 3; n++)
        {
            float angle = n * Mathf.Tau / 3;
            Lobe(leaves, top + new Vector3(Mathf.Cos(angle) * .22f, -.3f, Mathf.Sin(angle) * .22f), new(.16f, .18f, .16f), Palette.World.Coconut, top.Y, top.Y, midrib: true);
        }

        return Finish(trunk, PalmBark, leaves, FrondLeaves);
    }

    private static ShaderMaterial Foliage(bool leaflets, float lumps, float sway)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(FoliageShaderPath) };
        material.SetShaderParameter("leaflets", leaflets);
        material.SetShaderParameter("lumps", lumps);
        material.SetShaderParameter("sway", sway);
        return material;
    }

    private static SurfaceTool Begin()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        return tool;
    }

    private static ArrayMesh Finish(SurfaceTool? trunk, Material barkMaterial, SurfaceTool leaves, Material leafMaterial)
    {
        var mesh = new ArrayMesh();
        int surface = 0;
        if (trunk != null)
        {
            trunk.Commit(mesh);
            mesh.SurfaceSetMaterial(surface++, barkMaterial);
        }

        leaves.Commit(mesh);
        mesh.SurfaceSetMaterial(surface, leafMaterial);
        return mesh;
    }

    /// <summary>A crown of lumpy lobes: one in the middle and <paramref name="around"/> ringed about it.</summary>
    private static void Crown(SurfaceTool leaves, Random random, Vector3 center, float radius, float depth, int around, float spread, float low, float high)
    {
        Lobe(leaves, center, new(radius, depth, radius), Colors.White, low, high);
        for (int i = 0; i < around; i++)
        {
            float angle = i * Mathf.Tau / around + random.NextSingle() * .6f;
            float size = radius * (.62f + random.NextSingle() * .2f);
            var at = center + new Vector3(Mathf.Cos(angle) * spread, (random.NextSingle() - .6f) * depth, Mathf.Sin(angle) * spread);
            // Lobes low in the crown are a shade darker.
            var shade = Colors.White.Darkened(at.Y < center.Y ? .12f : 0);
            Lobe(leaves, at, new(size, depth * .85f, size), shade, low, high);
        }
    }

    /// <summary>An ellipsoid. Its UV2.y is its height between <paramref name="low"/> and <paramref name="high"/>.</summary>
    private static void Lobe(SurfaceTool tool, Vector3 center, Vector3 radii, Color color, float low, float high, bool midrib = false)
    {
        Vector3 Point(int ring, int segment, out Vector3 normal)
        {
            float phi = Mathf.Pi * ring / LobeRings;
            float theta = Mathf.Tau * segment / LobeSegments;
            var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), -Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
            normal = new Vector3(unit.X / radii.X, unit.Y / radii.Y, unit.Z / radii.Z).Normalized();
            return center + unit * radii;
        }

        for (int r = 0; r < LobeRings; r++)
        {
            for (int s = 0; s < LobeSegments; s++)
            {
                foreach (var (ring, segment) in Quad(r, s))
                {
                    var p = Point(ring, segment, out var normal);
                    float crown = high > low ? Mathf.Clamp((p.Y - low) / (high - low), 0, 1) : 0;
                    Vertex(tool, p, normal, color, midrib ? new(.5f, .5f) : new((float)segment / LobeSegments, (float)ring / LobeRings), crown);
                }
            }
        }
    }

    /// <summary>A palm frond: a strip rising from the crown and arching over and down, widest a third of the way out.</summary>
    private static void Frond(SurfaceTool tool, Vector3 root, float azimuth, float droop, Color color)
    {
        var outward = new Vector3(Mathf.Cos(azimuth), 0, Mathf.Sin(azimuth));
        var side = outward.Cross(Vector3.Up);
        Vector3 Spine(float s) => root + outward * (FrondLength * s) + Vector3.Up * (FrondLength * (.45f * s - droop * s * s));
        float Width(float s) => FrondWidth * Mathf.Sin(Mathf.Pi * Mathf.Min(1, .15f + s)) * (1 - s * .5f);

        for (int i = 0; i < FrondStations; i++)
        {
            float a = (float)i / FrondStations, b = (float)(i + 1) / FrondStations;
            var ahead = (Spine(b) - Spine(a)).Normalized();
            var normal = side.Cross(ahead).Normalized();
            if (normal.Y < 0)
            {
                normal = -normal;
            }

            (Vector3 Position, Vector2 Uv, float Along) Edge(float s, float across)
            {
                var p = Spine(s) + side * ((across - .5f) * Width(s)) + Vector3.Down * (Mathf.Abs(across - .5f) * 2 * FrondFold);
                return (p, new(across, s), s);
            }

            (Vector3 Position, Vector2 Uv, float Along)[] corners = [Edge(a, 0), Edge(a, 1), Edge(b, 1), Edge(a, 0), Edge(b, 1), Edge(b, 0)];
            foreach (var (position, uv, along) in corners)
            {
                Vertex(tool, position, normal, color, uv, along);
            }
        }
    }

    /// <summary>A tapering tube through <paramref name="path"/>, with <paramref name="ringColor"/> giving each ring its colour.</summary>
    private static void Tube(SurfaceTool tool, IReadOnlyList<Vector3> path, IReadOnlyList<float> radii, Func<int, Color> ringColor)
    {
        Vector3 Around(int ring, int side)
        {
            var along = (path[Math.Min(ring + 1, path.Count - 1)] - path[Math.Max(ring - 1, 0)]).Normalized();
            var across = along.Cross(Mathf.Abs(along.Dot(Vector3.Forward)) > .9f ? Vector3.Right : Vector3.Forward).Normalized();
            return across.Rotated(along, Mathf.Tau * side / TrunkSides);
        }

        for (int ring = 0; ring < path.Count - 1; ring++)
        {
            for (int side = 0; side < TrunkSides; side++)
            {
                foreach (var (r, s) in Quad(ring, side))
                {
                    var normal = Around(r, s);
                    Vertex(tool, path[r] + normal * radii[r], normal, ringColor(r), new((float)s / TrunkSides, r), 0);
                }
            }
        }
    }

    /// <summary>The six corners of the two triangles joining (a, b) to (a + 1, b + 1).</summary>
    private static (int, int)[] Quad(int a, int b) => [(a, b), (a + 1, b), (a + 1, b + 1), (a, b), (a + 1, b + 1), (a, b + 1)];

    private static void Vertex(SurfaceTool tool, Vector3 position, Vector3 normal, Color color, Vector2 uv, float crown)
    {
        tool.SetColor(color);
        tool.SetNormal(normal);
        tool.SetUV(uv);
        tool.SetUV2(new(0, crown));
        tool.AddVertex(position);
    }
}
