namespace Breakwater;

/// <summary>Curved trunks and individually modelled pinnate leaflets, shared by many palms.</summary>
public static class ArtStudyPalm
{
    private const int TrunkRings = 28, TrunkSides = 12, FrondCount = 15, LeafletPairs = 23;
    private const float TrunkHeight = 9.8f, TrunkLean = 1.1f, FrondLength = 4.4f;

    public static ArrayMesh Build(int variant)
    {
        var random = new Random(71 + variant);
        var trunk = new SurfaceTool();
        trunk.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Centre(float t) => new(TrunkLean * t * t, TrunkHeight * t, .28f * Mathf.Sin(t * 2));
        for (int ring = 0; ring < TrunkRings; ring++)
        {
            for (int side = 0; side < TrunkSides; side++)
            {
                foreach (var (r, s) in (ValueTuple<int, int>[])[(ring, side), (ring + 1, side), (ring + 1, side + 1), (ring, side), (ring + 1, side + 1), (ring, side + 1)])
                {
                    float t = r / (float)TrunkRings;
                    float angle = s * Mathf.Tau / TrunkSides;
                    var normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    float radius = Mathf.Lerp(.25f, .13f, t) * (r % 2 == 0 ? 1 : .95f);
                    trunk.SetNormal(normal);
                    trunk.SetUV(new(s / (float)TrunkSides, t * 9));
                    trunk.AddVertex(Centre(t) + normal * radius);
                }
            }
        }
        trunk.GenerateTangents();
        var mesh = trunk.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            AlbedoTexture = SurfaceTextures.Albedo(SurfaceTextures.PalmBark),
            AlbedoColor = Palette.Art.Bark.Lightened(.15f),
            Roughness = .95f,
            MetallicSpecular = .1f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        var leaves = new SurfaceTool();
        leaves.Begin(Mesh.PrimitiveType.Triangles);
        var crown = Centre(1);
        for (int frond = 0; frond < FrondCount; frond++)
        {
            float azimuth = frond * 2.39996f + variant;
            var forward = new Vector3(Mathf.Cos(azimuth), 0, Mathf.Sin(azimuth));
            var right = new Vector3(-forward.Z, 0, forward.X);
            float length = FrondLength * (.85f + random.NextSingle() * .3f);
            float rise = frond < 5 ? 3.2f : frond < 10 ? 1.3f : .2f;
            float droop = 2.5f + random.NextSingle();
            Vector3 Spine(float t) => crown + forward * (length * t) + Vector3.Up * (rise * Mathf.Sin(t * Mathf.Pi / 2) - droop * t * t);
            var color = Palette.Art.Leaf.Lerp(Palette.Art.LeafTip, random.NextSingle() * .55f);
            for (int pair = 1; pair <= LeafletPairs; pair++)
            {
                float t = pair / (float)(LeafletPairs + 1);
                float leafletLength = .95f * Mathf.Pow(Mathf.Sin(t * Mathf.Pi), .65f);
                var basePoint = Spine(t);
                foreach (int side in (int[])[-1, 1])
                {
                    var mid = basePoint + right * (side * leafletLength * .6f) + forward * .2f - Vector3.Up * .06f;
                    var tip = basePoint + right * (side * leafletLength) + forward * .42f - Vector3.Up * (.2f + t * .15f);
                    float halfWidth = .115f * Mathf.Sin(t * Mathf.Pi);
                    AddLeaf(leaves, basePoint, mid - forward * halfWidth, tip, color);
                    AddLeaf(leaves, basePoint, tip, mid + forward * halfWidth, color.Lightened(.06f));
                }
                var next = Spine(t + .04f);
                AddLeaf(leaves, basePoint - right * .02f, basePoint + right * .02f, next + right * .012f, color);
                AddLeaf(leaves, basePoint - right * .02f, next + right * .012f, next - right * .012f, color);
            }
        }
        leaves.Commit(mesh);
        mesh.SurfaceSetMaterial(1, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = .85f,
            MetallicSpecular = .12f,
        });
        return mesh;
    }

    private static void AddLeaf(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        var normal = (b - a).Cross(c - a).Normalized();
        if (normal.Y < 0)
        {
            normal = -normal;
        }
        foreach (var point in (Vector3[])[a, b, c])
        {
            tool.SetColor(color);
            tool.SetNormal(normal);
            tool.AddVertex(point);
        }
    }
}
