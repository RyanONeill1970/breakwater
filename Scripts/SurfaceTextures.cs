namespace Breakwater;

/// <summary>
/// The photographed surface textures in <c>assets/textures</c> (Poly Haven, CC0): each a colour map,
/// an OpenGL-style normal map and an AO/roughness/metal map, 1K. <see cref="Material"/> lays one over
/// a mesh in world space, so it tiles at a fixed size in metres whatever the mesh's shape.
/// </summary>
public static class SurfaceTextures
{
    public const string Grass = "leafy_grass";
    public const string Sand = "coast_sand_01";
    public const string ForestFloor = "forest_ground_04";
    public const string Rock = "rock_face";
    public const string Asphalt = "asphalt_02";
    public const string Stucco = "white_stucco";
    public const string Concrete = "dirty_concrete";
    public const string Bark = "bark_brown_02";
    public const string PalmBark = "palm_tree_bark";

    private const string Folder = "res://assets/textures";

    private const float Specular = .15f;

    /// <summary>The photographs' relief is softened: seen from above at a distance, full strength reads as noise.</summary>
    private const float NormalStrength = .5f;

    public static Texture2D Albedo(string asset) => Load(asset, "diff");

    public static Texture2D Normal(string asset) => Load(asset, "nor_gl");

    /// <summary>Ambient occlusion, roughness and metal in the red, green and blue channels.</summary>
    public static Texture2D Orm(string asset) => Load(asset, "arm");

    /// <summary>A material showing <paramref name="asset"/> in world space, one repeat every <paramref name="metres"/>, tinted by <paramref name="tint"/>.</summary>
    public static OrmMaterial3D Material(string asset, float metres, Color tint) => new()
    {
        AlbedoColor = tint,
        AlbedoTexture = Albedo(asset),
        NormalEnabled = true,
        NormalScale = NormalStrength,
        NormalTexture = Normal(asset),
        OrmTexture = Orm(asset),
        // Natural surfaces shine little; full specular makes every bump of the normal map glint in the sun.
        MetallicSpecular = Specular,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        Uv1Scale = Vector3.One / metres,
        Uv1Triplanar = true,
        Uv1WorldTriplanar = true,
    };

    /// <summary>
    /// A material showing <paramref name="asset"/> tinted so its average colour is <paramref name="colour"/>:
    /// the photograph's grain and relief in the game's own colours.
    /// </summary>
    public static OrmMaterial3D Coloured(string asset, float metres, Color colour) => Material(asset, metres, TintTo(asset, colour));

    /// <summary>The tint that turns <paramref name="asset"/>'s average colour into <paramref name="colour"/>.</summary>
    public static Color TintTo(string asset, Color colour)
    {
        var mean = Mean(asset);
        return new(colour.R / mean.R, colour.G / mean.G, colour.B / mean.B);
    }

    /// <summary>The average colour of <paramref name="asset"/>'s colour map, to divide by so it adds only grain.</summary>
    public static Color Mean(string asset)
    {
        var image = Albedo(asset).GetImage();
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        // Halving at a time averages every pixel; one resize straight to 1 × 1 would sample only a few.
        while (image.GetWidth() > 1 || image.GetHeight() > 1)
        {
            image.Resize(Math.Max(1, image.GetWidth() / 2), Math.Max(1, image.GetHeight() / 2), Image.Interpolation.Bilinear);
        }
        return image.GetPixel(0, 0);
    }

    private static Texture2D Load(string asset, string map) => GD.Load<Texture2D>($"{Folder}/{asset}/{asset}_{map}_1k.jpg");
}
