namespace Breakwater;

public static class IslandGroundCover
{
    public static ArrayMesh BuildGrassTuft()
    {
        var tool = new SurfaceTool();
        var random = new Random(927);
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (int blade = 0; blade < 14; blade++)
        {
            float angle = random.NextSingle() * Mathf.Tau;
            var lean = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            var side = new Vector3(lean.Z, 0, -lean.X);
            var start = new Vector3(random.NextSingle() - .5f, 0, random.NextSingle() - .5f) * .6f;
            float height = .35f + random.NextSingle() * .5f;
            for (int segment = 0; segment < 3; segment++)
            {
                float t = segment / 3f, next = (segment + 1) / 3f;
                Vector3 Point(float along, float across) => start + Vector3.Up * (height * along)
                    + lean * (height * along * along * .55f) + side * (across * .05f * (1 - along));
                foreach (var uv in (Vector2[])[new(0, t), new(1, t), new(1, next), new(0, t), new(1, next), new(0, next)])
                {
                    tool.SetColor(Palette.Art.Grass.Lerp(Palette.Art.LeafTip, uv.Y * .6f).Darkened((1 - uv.Y) * .38f));
                    tool.SetNormal((lean * .25f + Vector3.Up).Normalized());
                    tool.SetUV(uv);
                    tool.AddVertex(Point(uv.Y, uv.X * 2 - 1));
                }
            }
        }
        var mesh = tool.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 1,
            MetallicSpecular = 0,
        });
        return mesh;
    }
}
