namespace Breakwater;

/// <summary>
/// Builds one vertex-coloured mesh out of simple solids: boxes, extruded profiles, lofts, cylinders
/// and open bands. Flat faces are shaded flat and cylinders smooth. Every face's normal is turned
/// to point away from a point inside its solid, so no care over winding is needed; materials for
/// these meshes draw both sides. Vertex alpha is passed through for the material to use.
/// </summary>
public sealed class MeshBuilder
{
    private readonly SurfaceTool tool = new();

    public MeshBuilder() => tool.Begin(Mesh.PrimitiveType.Triangles);

    public ArrayMesh Commit() => tool.Commit();

    public void Box(Vector3 center, Vector3 size, Color color)
    {
        var h = size / 2;
        Vector2[] square = [new(-h.Z, -h.Y), new(h.Z, -h.Y), new(h.Z, h.Y), new(-h.Z, h.Y)];
        Prism(square.Select(p => p + new Vector2(center.Z, center.Y)).ToArray(), center.X - h.X, center.X + h.X, color);
    }

    /// <summary>
    /// A convex outline in the Z–Y plane (x across is Z, y is up), extruded across X from
    /// <paramref name="left"/> to <paramref name="right"/>: hull sides, skirts and the like.
    /// </summary>
    public void Prism(IReadOnlyList<Vector2> profile, float left, float right, Color color)
    {
        Vector3 At(Vector2 p, float x) => new(x, p.Y, p.X);
        var inside = Centroid(profile.Select(p => At(p, (left + right) / 2)));
        Face(profile.Select(p => At(p, left)).ToArray(), inside, color);
        Face(profile.Select(p => At(p, right)).ToArray(), inside, color);
        for (int i = 0; i < profile.Count; i++)
        {
            var a = profile[i];
            var b = profile[(i + 1) % profile.Count];
            Face([At(a, left), At(b, left), At(b, right), At(a, right)], inside, color);
        }
    }

    /// <summary>A convex solid between two outlines with matching corners: turrets, cabs, cupolas.</summary>
    public void Loft(IReadOnlyList<Vector3> bottom, IReadOnlyList<Vector3> top, Color color)
    {
        var inside = Centroid(bottom.Concat(top));
        Face([.. bottom], inside, color);
        Face([.. top], inside, color);
        for (int i = 0; i < bottom.Count; i++)
        {
            int j = (i + 1) % bottom.Count;
            Face([bottom[i], bottom[j], top[j], top[i]], inside, color);
        }
    }

    /// <summary>
    /// A closed loop in the Z–Y plane swept across X, without end caps: a track run round its
    /// wheels, open at the sides so the wheels show through.
    /// </summary>
    public void Band(IReadOnlyList<Vector2> loop, float left, float right, Color color)
    {
        var inside = Centroid(loop.Select(p => new Vector3((left + right) / 2, p.Y, p.X)));
        for (int i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            Face([new(left, a.Y, a.X), new(left, b.Y, b.X), new(right, b.Y, b.X), new(right, a.Y, a.X)], inside, color);
        }
    }

    /// <summary>A capped cylinder or cone from <paramref name="from"/> to <paramref name="to"/>, smooth-shaded round its side.</summary>
    public void Cylinder(Vector3 from, Vector3 to, float fromRadius, float toRadius, int sides, Color color)
    {
        var axis = (to - from).Normalized();
        var across = axis.Cross(Mathf.Abs(axis.Dot(Vector3.Up)) > .9f ? Vector3.Right : Vector3.Up).Normalized();
        Vector3 Spoke(int i) => across.Rotated(axis, Mathf.Tau * i / sides);
        for (int i = 0; i < sides; i++)
        {
            var (a, b) = (Spoke(i), Spoke(i + 1));
            (Vector3 Position, Vector3 Normal)[] corners =
            [
                (from + a * fromRadius, a), (to + a * toRadius, a), (to + b * toRadius, b),
                (from + a * fromRadius, a), (to + b * toRadius, b), (from + b * fromRadius, b),
            ];
            foreach (var (position, normal) in corners)
            {
                Vertex(position, normal, color);
            }
        }

        var center = (from + to) / 2;
        Face([.. Enumerable.Range(0, sides).Select(i => from + Spoke(i) * fromRadius)], center, color);
        if (toRadius > 0)
        {
            Face([.. Enumerable.Range(0, sides).Select(i => to + Spoke(i) * toRadius)], center, color);
        }
    }

    /// <summary>A wheel on an axle along X: a tyre and a lighter hub disc on its outer face.</summary>
    public void Wheel(Vector3 center, float radius, float width, int sides, Color tyre, Color hub, int outward)
    {
        var half = Vector3.Right * (width / 2);
        Cylinder(center - half, center + half, radius, radius, sides, tyre);
        var face = center + Vector3.Right * (outward * (width / 2 + .01f));
        Cylinder(face, face + Vector3.Right * (outward * .03f), radius * .62f, radius * .5f, sides, hub);
    }

    /// <summary>A flat, convex polygon facing away from <paramref name="inside"/>.</summary>
    private void Face(Vector3[] polygon, Vector3 inside, Color color)
    {
        var normal = (polygon[1] - polygon[0]).Cross(polygon[2] - polygon[0]);
        if (normal.LengthSquared() < 1e-10f)
        {
            normal = (polygon[2] - polygon[1]).Cross(polygon[^1] - polygon[1]);
        }

        normal = normal.Normalized();
        if (normal.Dot(Centroid(polygon) - inside) < 0)
        {
            normal = -normal;
        }

        for (int i = 1; i < polygon.Length - 1; i++)
        {
            Vertex(polygon[0], normal, color);
            Vertex(polygon[i], normal, color);
            Vertex(polygon[i + 1], normal, color);
        }
    }

    private void Vertex(Vector3 position, Vector3 normal, Color color)
    {
        tool.SetColor(color);
        tool.SetNormal(normal);
        tool.AddVertex(position);
    }

    private static Vector3 Centroid(IEnumerable<Vector3> points)
    {
        var list = points.ToList();
        return list.Aggregate(Vector3.Zero, (sum, p) => sum + p) / list.Count;
    }
}
