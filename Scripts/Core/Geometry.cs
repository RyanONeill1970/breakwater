namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// Planar geometry shared by navigation, collision, sight and ballistics. Headings are radians,
/// with 0 facing north (−Y) and positive angles turning west.
/// </summary>
public static class Geometry
{
    // Below this a direction component is treated as zero, to avoid dividing by it.
    private const float ParallelEpsilon = .00001f;

    // Squared segment length below which a sweep is treated as a point.
    private const float DegenerateSegment = .000001f;

    public static Vector2 Forward(float angle) => new(-MathF.Sin(angle), -MathF.Cos(angle));

    public static float Angle(Vector2 direction) => MathF.Atan2(-direction.X, -direction.Y);

    /// <summary>Signed shortest turn from <paramref name="from"/> to <paramref name="to"/>, in (−π, π].</summary>
    public static float Delta(float from, float to) => MathF.IEEERemainder(to - from, MathF.Tau);

    public static float Turn(float from, float to, float max) => from + Math.Clamp(Delta(from, to), -max, max);

    public static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);

    /// <summary>Distance from <paramref name="p"/> to the nearest point of the segment <paramref name="a"/>–<paramref name="b"/>.</summary>
    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared < DegenerateSegment ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>
    /// Sweeps the segment <paramref name="from"/>→<paramref name="to"/> against the box grown by
    /// <paramref name="padding"/>. Returns the entry fraction in [0, 1], or null for no contact.
    /// </summary>
    public static float? HitBox(Vector2 from, Vector2 to, Obstacle box, float padding = 0)
    {
        var min = box.Center - box.HalfSize - new Vector2(padding);
        var max = box.Center + box.HalfSize + new Vector2(padding);
        var delta = to - from;
        float enter = 0, exit = 1;
        for (int axis = 0; axis < 2; axis++)
        {
            float p = axis == 0 ? from.X : from.Y;
            float d = axis == 0 ? delta.X : delta.Y;
            float low = axis == 0 ? min.X : min.Y;
            float high = axis == 0 ? max.X : max.Y;
            if (Math.Abs(d) < ParallelEpsilon)
            {
                if (p < low || p > high)
                {
                    return null;
                }
            }
            else
            {
                float a = (low - p) / d;
                float b = (high - p) / d;
                if (a > b)
                {
                    (a, b) = (b, a);
                }

                enter = Math.Max(enter, a);
                exit = Math.Min(exit, b);
                if (enter > exit)
                {
                    return null;
                }
            }
        }
        return enter;
    }

    /// <summary>
    /// Sweeps the segment against a circle. Returns the entry fraction in [0, 1] (0 when the start
    /// is already inside), or null for no contact.
    /// </summary>
    public static float? HitCircle(Vector2 from, Vector2 to, Vector2 center, float radius)
    {
        var delta = to - from;
        var offset = from - center;
        float c = offset.LengthSquared() - radius * radius;
        if (c <= 0)
        {
            return 0;
        }

        float a = delta.LengthSquared();
        if (a < DegenerateSegment)
        {
            return null;
        }

        float b = Vector2.Dot(offset, delta);
        float disc = b * b - a * c;
        if (disc < 0)
        {
            return null;
        }

        float t = (-b - MathF.Sqrt(disc)) / a;
        return t is >= 0 and <= 1 ? t : null;
    }
}
