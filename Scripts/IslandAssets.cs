namespace Breakwater;

/// <summary>Scanned scenery shared by the art study and playable islands. Materials and bounds are cached.</summary>
public sealed class IslandAssets(float seaLevel = -.06f)
{
    public const string Boulder = "res://assets/art-study/boulder_01/boulder_01_2k.gltf";
    public const string Tree = "res://assets/art-study/island_tree_01/island_tree_01_1k.gltf";
    public const string Shrub = "res://assets/art-study/wild_rooibos_bush/wild_rooibos_bush_1k.gltf";
    public const string Fern = "res://assets/art-study/fern_02/fern_02_2k.gltf";
    private readonly Dictionary<string, (PackedScene Scene, Aabb Bounds)> assets = [];
    private readonly Dictionary<(string Path, int Surface), Material> materials = [];

    public Aabb Bounds(string path) => Load(path).Bounds;

    /// <summary>Places a scan at its bottom centre. A supplied box fits even the rotated mesh inside it.</summary>
    public Node3D Place(Node3D parent, string path, Vector3 position, Vector3 size, float yaw, bool fitBox = false)
    {
        var asset = Load(path);
        var bounds = asset.Bounds;
        var rotation = new Basis(Vector3.Up, Mathf.DegToRad(yaw));
        var rotated = new Transform3D(rotation, Vector3.Zero) * bounds;
        float width = size.X / Math.Max(bounds.Size.X, bounds.Size.Z);
        var scale = fitBox ? size / rotated.Size : new Vector3(width, size.Y / bounds.Size.Y, width);
        var basis = rotation.Scaled(scale);
        var centre = new Vector3(bounds.GetCenter().X, bounds.Position.Y, bounds.GetCenter().Z);
        var root = new Node3D { Transform = new(basis, position) };
        parent.AddChild(root);
        var offset = new Node3D { Position = -centre };
        root.AddChild(offset);
        var instance = asset.Scene.Instantiate<Node3D>();
        offset.AddChild(instance);
        foreach (var mesh in instance.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            mesh.LodBias = path == Tree ? .6f : .5f;
            if (mesh.Mesh == null)
            {
                continue;
            }
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                var key = (path, surface);
                if (!materials.TryGetValue(key, out var material) && mesh.GetActiveMaterial(surface) is BaseMaterial3D source)
                {
                    material = Dress(path, source);
                    materials[key] = material;
                }
                if (material != null)
                {
                    mesh.SetSurfaceOverrideMaterial(surface, material);
                }
            }
        }
        return root;
    }

    private Material Dress(string path, BaseMaterial3D source)
    {
        if (path == Boulder || path.Contains("coastal_cliff", StringComparison.Ordinal) || path.Contains("rock_face", StringComparison.Ordinal))
        {
            var scan = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtScan.gdshader") };
            scan.SetShaderParameter("color_tex", source.AlbedoTexture);
            scan.SetShaderParameter("normal_tex", source.NormalTexture);
            scan.SetShaderParameter("orm_tex", source.RoughnessTexture);
            scan.SetShaderParameter("stone_tint", Palette.Art.ScanTint);
            scan.SetShaderParameter("moss_color", Palette.Art.Grass);
            scan.SetShaderParameter("sea_level", seaLevel);
            return scan;
        }
        // Imported opaque/transparent surfaces can be reordered; identify leaves by their texture.
        if (source.AlbedoTexture is { } leafTexture &&
            ((path == Tree && leafTexture.ResourcePath.Contains("_leaves_", StringComparison.Ordinal)) || path == Shrub || path == Fern))
        {
            var leaves = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtLeaves.gdshader") };
            leaves.SetShaderParameter("leaf_tex", leafTexture);
            leaves.SetShaderParameter("leaf_color", path == Fern ? Palette.Art.Leaf : Palette.Art.LeafTip);
            return leaves;
        }
        var material = (BaseMaterial3D)source.Duplicate();
        material.MetallicSpecular = .1f;
        if (material.Transparency != BaseMaterial3D.TransparencyEnum.Disabled)
        {
            material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            material.AlphaScissorThreshold = .4f;
        }
        return material;
    }

    private (PackedScene Scene, Aabb Bounds) Load(string path)
    {
        if (assets.TryGetValue(path, out var asset))
        {
            return asset;
        }
        var scene = GD.Load<PackedScene>(path);
        var probe = scene.Instantiate<Node3D>();
        var bounds = new Aabb();
        bool first = true;
        void Measure(Node node, Transform3D parent)
        {
            var transform = node is Node3D spatial ? parent * spatial.Transform : parent;
            if (node is MeshInstance3D { Mesh: { } mesh })
            {
                var box = transform * mesh.GetAabb();
                bounds = first ? box : bounds.Merge(box);
                first = false;
            }
            foreach (var child in node.GetChildren())
            {
                Measure(child, transform);
            }
        }
        Measure(probe, Transform3D.Identity);
        probe.Free();
        asset = (scene, bounds);
        assets.Add(path, asset);
        return asset;
    }
}
