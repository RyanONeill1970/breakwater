namespace Breakwater;

/// <summary>
/// Vehicle models from <c>assets/models</c> (CC-BY; see <c>assets/CREDITS.md</c>), fitted to the game:
/// turned to face −Z, scaled to the real vehicle's length times <see cref="GameScale"/>, stood on
/// the ground, and cut in two, hull and turret, so the turret can traverse. Most of these models are
/// one merged mesh, so the cut is geometric: triangles above the turret ring and near the turret's
/// centre, or in the narrow band ahead of it where the gun lies, go to the turret. Each kind is
/// built once and shared. A kind with no model returns null, and keeps its procedural body.
/// </summary>
public static class VehicleAssets
{
    /// <summary>Game metres per real metre: the battlefield is drawn smaller than life so a battle fits on screen.</summary>
    public const float GameScale = .55f;

    /// <summary>How brightly hulls glow at night, as a multiple of their paint.</summary>
    private const float HeatGlow = .9f;

    /// <summary>Roughness for a repainted model: matt military paint.</summary>
    private const float PaintRoughness = .75f;

    /// <summary>A tank's gun reaches further ahead than this fraction of its length from the middle; nothing else above the ring does.</summary>
    private const float GunReach = .38f;

    /// <summary>Lightens the M113's and T-62's near-black grey textures to olive drab.</summary>
    private static readonly Color DarkTextureTint = new(1.3f, 1.4f, 1.05f);


    private static readonly Dictionary<VehicleKind, ModelSpec> Specs = new()
    {
        [VehicleKind.Abrams] = new("res://assets/models/abrams/scene.gltf", 90, 9.83f, .36f, .02f, .2f, .03f),
        [VehicleKind.MainTank] = new("res://assets/models/abrams/scene.gltf", 90, 9.83f, .36f, .02f, .2f, .03f),
        [VehicleKind.Bradley] = new("res://assets/models/bradley/scene.gltf", 90, 6.55f, .47f, .05f, .2f, .03f),
        [VehicleKind.Scout] = new("res://assets/models/bradley/scene.gltf", 90, 6.55f, .47f, .05f, .2f, .03f),
        [VehicleKind.M113] = new("res://assets/models/m113/scene.gltf", 180, 4.86f, .8f, 0, .12f, .02f, DarkTextureTint),
        [VehicleKind.Itv] = new("res://assets/models/m113/scene.gltf", 180, 4.86f, .8f, 0, .12f, .02f, DarkTextureTint),
        [VehicleKind.T72] = new("res://assets/models/t72/scene.gltf", -90, 9.53f, .3f, .05f, .18f, .03f),
        [VehicleKind.Defender] = new("res://assets/models/t72/scene.gltf", -90, 9.53f, .3f, .05f, .18f, .03f),
        [VehicleKind.T62] = new("res://assets/models/t62/scene.gltf", 90, 9.34f, .5f, .05f, .18f, .03f, DarkTextureTint),
        [VehicleKind.Bmp] = new("res://assets/models/bmp2/scene.gltf", 0, 6.72f, .6f, .05f, .15f, .02f, Palette.World.CarrierHull),
        [VehicleKind.Btr] = new("res://assets/models/btr60/scene.gltf", 90, 7.56f, .6f, 0, .08f, .02f),
        [VehicleKind.Truck] = new("res://assets/models/m35/scene.gltf", 0, 6.71f, 2, 0, 0, 0),
    };

    private static readonly Dictionary<(VehicleKind, bool), Fitted?> Cache = [];

    /// <summary>The model for <paramref name="kind"/>, glowing faintly with engine heat <paramref name="atNight"/>; null if it has none.</summary>
    public static Fitted? For(VehicleKind kind, bool atNight)
    {
        if (!Cache.TryGetValue((kind, atNight), out var fitted))
        {
            fitted = Specs.TryGetValue(kind, out var spec) ? Fit(spec, atNight) : null;
            Cache[(kind, atNight)] = fitted;
        }

        return fitted;
    }

    private static Fitted? Fit(ModelSpec spec, bool atNight)
    {
        if (ResourceLoader.Load<PackedScene>(spec.Path) is not { } scene)
        {
            GD.PushError($"Vehicle model missing: {spec.Path}");
            return null;
        }

        var root = scene.Instantiate<Node3D>();
        List<Triangle> triangles = [];
        Collect(root, Transform3D.Identity, triangles);
        root.Free();

        // Face −Z, then scale to size and stand the model on the ground, centred.
        var turn = new Basis(Vector3.Up, Mathf.DegToRad(spec.Yaw));
        var box = Bounds(triangles.SelectMany(t => t.Corners).Select(c => turn * c.Position));
        float scale = spec.Length * GameScale / box.Size.Z;
        var shift = new Vector3(-(box.Position.X + box.Size.X / 2), -box.Position.Y, -(box.Position.Z + box.Size.Z / 2)) * scale;
        var place = new Transform3D(turn.Scaled(Vector3.One * scale), shift);
        var size = box.Size * scale;
        GD.Print($"Vehicle model {spec.Path}: {size.X:0.00} x {size.Y:0.00} x {size.Z:0.00} m fitted, {triangles.Count} triangles");

        // The turret turns on the line of its gun, which isn't always the hull's centre line: the gun is
        // what reaches out ahead of the hull above the turret ring.
        float ring = size.Y * spec.TurretRing;
        var gunLine = triangles.SelectMany(t => t.Corners).Select(c => place * c.Position)
            .Where(p => p.Y > ring && p.Z < -size.Z * GunReach).Select(p => p.X).Order().ToList();
        var pivot = new Vector3(gunLine.Count > 0 ? gunLine[gunLine.Count / 2] : 0, ring, size.Z * spec.TurretCenter);
        float reach = size.Z * spec.TurretRadius;
        float gun = size.Z * spec.GunWidth;
        bool InTurret(Vector3 c) => c.Y > pivot.Y &&
            (new Vector2(c.X - pivot.X, c.Z - pivot.Z).Length() < reach || (c.Z < pivot.Z && Mathf.Abs(c.X - pivot.X) < gun));

        // Each source material becomes a painted one, made once per model.
        var finishes = triangles.Select(t => t.Material).Distinct().ToDictionary(m => m, m => Finish(m, spec.Repaint, atNight));
        var hull = new Dictionary<Material, SurfaceTool>();
        var turret = new Dictionary<Material, SurfaceTool>();
        var muzzle = pivot;
        foreach (var triangle in triangles)
        {
            var corners = triangle.Corners.Select(c => c with { Position = place * c.Position, Normal = (place.Basis * c.Normal).Normalized() }).ToArray();
            var centre = (corners[0].Position + corners[1].Position + corners[2].Position) / 3;
            bool onTurret = InTurret(centre);

            var target = onTurret ? turret : hull;
            var finish = finishes[triangle.Material];
            if (!target.TryGetValue(finish, out var tool))
            {
                tool = new SurfaceTool();
                tool.Begin(Mesh.PrimitiveType.Triangles);
                target[finish] = tool;
            }

            foreach (var corner in corners)
            {
                tool.SetNormal(corner.Normal);
                tool.SetUV(corner.Uv);
                tool.AddVertex(onTurret ? corner.Position - pivot : corner.Position);
                if (onTurret && Mathf.Abs(corner.Position.X - pivot.X) < gun && corner.Position.Z < muzzle.Z)
                {
                    muzzle = corner.Position;
                }
            }
        }

        // The muzzle: the gun's most forward point, on the line of the gun.
        return new(Build(hull), turret.Count == 0 ? null : Build(turret), pivot, (muzzle with { X = pivot.X }) - pivot);
    }

    private static ArrayMesh Build(Dictionary<Material, SurfaceTool> surfaces)
    {
        var mesh = new ArrayMesh();
        foreach (var (material, tool) in surfaces)
        {
            tool.GenerateTangents();
            tool.Commit(mesh);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, material);
        }

        return mesh;
    }

    /// <summary>
    /// A copy of <paramref name="material"/> as paint rather than bare metal (several models mark
    /// painted armour metallic, which reads as black under a sky), in <paramref name="repaint"/> if
    /// given, and glowing with its own colour <paramref name="atNight"/> for the thermal sight.
    /// </summary>
    private static Material Finish(Material material, Color? repaint, bool atNight)
    {
        if (material.Duplicate() is not BaseMaterial3D paint)
        {
            return material;
        }

        paint.Metallic = 0;
        paint.MetallicTexture = null;
        if (repaint is { } color)
        {
            paint.AlbedoColor = color;
            if (paint.AlbedoTexture == null)
            {
                paint.Roughness = PaintRoughness;
            }
        }

        if (atNight)
        {
            paint.EmissionEnabled = true;
            paint.Emission = paint.AlbedoColor;
            paint.EmissionTexture = paint.AlbedoTexture;
            paint.EmissionEnergyMultiplier = HeatGlow;
        }
        else
        {
            paint.AlbedoColor *= Palette.Art.Armour;
            paint.MetallicSpecular = .15f;
            paint.Roughness = .85f;
        }

        return paint;
    }

    private static void Collect(Node node, Transform3D parent, List<Triangle> triangles)
    {
        var transform = node is Node3D spatial ? parent * spatial.Transform : parent;
        if (node is MeshInstance3D { Mesh: { } mesh } instance && instance.Visible)
        {
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if (mesh is ArrayMesh array && array.SurfaceGetPrimitiveType(s) != Mesh.PrimitiveType.Triangles)
                {
                    continue;
                }

                var arrays = mesh.SurfaceGetArrays(s);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                var uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                var material = instance.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s) ?? new StandardMaterial3D();
                // Normals take the inverse transpose, so they stay square to surfaces under uneven scaling.
                var normalBasis = transform.Basis.Inverse().Transposed();
                int count = indices.Length > 0 ? indices.Length : vertices.Length;
                for (int i = 0; i + 2 < count; i += 3)
                {
                    Corner At(int k)
                    {
                        int v = indices.Length > 0 ? indices[i + k] : i + k;
                        var normal = normals.Length > v ? normals[v] : Vector3.Up;
                        return new(transform * vertices[v], (normalBasis * normal).Normalized(), uvs.Length > v ? uvs[v] : Vector2.Zero);
                    }

                    triangles.Add(new([At(0), At(1), At(2)], material));
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Collect(child, transform, triangles);
        }
    }

    private static Aabb Bounds(IEnumerable<Vector3> points)
    {
        var list = points.ToList();
        var box = new Aabb(list[0], Vector3.Zero);
        foreach (var p in list)
        {
            box = box.Expand(p);
        }

        return box;
    }

    /// <summary>A fitted model: hull and turret meshes, the turret's pivot on the hull, and the muzzle relative to the pivot.</summary>
    public sealed record Fitted(ArrayMesh Hull, ArrayMesh? Turret, Vector3 Pivot, Vector3 Muzzle);

    private sealed record Corner(Vector3 Position, Vector3 Normal, Vector2 Uv);

    private sealed record Triangle(Corner[] Corners, Material Material);
}
