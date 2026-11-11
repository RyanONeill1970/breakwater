namespace Breakwater;

/// <summary>Combines static prop parts by material, keeping detail from becoming hundreds of draw calls.</summary>
public sealed class ArtStudyBatch
{
    private readonly Dictionary<Material, SurfaceTool> surfaces = [];

    public void Add(Mesh mesh, Transform3D transform, Material material)
    {
        if (!surfaces.TryGetValue(material, out var tool))
        {
            tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            surfaces.Add(material, tool);
        }
        var normalBasis = transform.Basis.Inverse().Transposed();
        for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            var arrays = mesh.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            int count = indices.Length > 0 ? indices.Length : vertices.Length;
            for (int i = 0; i < count; i++)
            {
                int index = indices.Length > 0 ? indices[i] : i;
                tool.SetNormal((normalBasis * normals[index]).Normalized());
                tool.SetUV(uvs.Length > index ? uvs[index] : Vector2.Zero);
                tool.AddVertex(transform * vertices[index]);
            }
        }
    }

    public void Box(Vector3 position, Vector3 size, Material material, float yaw = 0) =>
        Add(new BoxMesh { Size = size }, new(new Basis(Vector3.Up, yaw), position), material);

    public void Beam(Vector3 from, Vector3 to, float radius, Material material, int sides = 10)
    {
        var delta = to - from;
        Add(new CylinderMesh { BottomRadius = radius, TopRadius = radius, Height = delta.Length(), RadialSegments = sides, Rings = 1 },
            new(new Basis(new Quaternion(Vector3.Up, delta.Normalized())), (from + to) / 2), material);
    }

    public void Ring(Vector3 position, float inner, float outer, Material material, Vector3 rotation)
    {
        Add(new TorusMesh { InnerRadius = inner, OuterRadius = outer, Rings = 20, RingSegments = 8 },
            new(Basis.FromEuler(rotation), position), material);
    }

    public void Commit(Node3D parent)
    {
        var mesh = new ArrayMesh();
        foreach (var (material, tool) in surfaces)
        {
            tool.GenerateTangents();
            tool.Index();
            tool.Commit(mesh);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, material);
        }
        parent.AddChild(new MeshInstance3D { Mesh = mesh });
        foreach (var tool in surfaces.Values)
        {
            tool.Dispose();
        }
        surfaces.Clear();
    }
}
