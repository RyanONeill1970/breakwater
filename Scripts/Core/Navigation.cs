namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// A* over a square grid covering the island, with hull clearance and line-of-sight smoothing.
/// Movement and collision use the same obstacle footprints, in metres. With Classic
/// <paramref name="terrain"/>, routes minimise travel time rather than distance, so they follow roads
/// and skirt jungle and rivers.
/// </summary>
public sealed class Navigation(IReadOnlyList<Obstacle> obstacles, TerrainMap? terrain = null)
{
    /// <summary>Grid spacing in metres.</summary>
    private const float Cell = 2;

    /// <summary>Cells per side: enough to span −Edge..+Edge inclusive.</summary>
    private const int Size = (int)(2 * Mission.Edge / Cell) + 1;

    /// <summary>A grid cell is walkable if a hull this wide fits there; a little over the hull radius.</summary>
    private const float Clearance = Mission.VehicleRadius + .5f;

    /// <summary>Destinations are pulled this far in from the coast so the hull fits.</summary>
    private const float GoalInset = 4;

    /// <summary>Other vehicles block a planning cell within this distance of their centre.</summary>
    private const float TrafficCellRadius = 5;

    /// <summary>Route segments must pass other vehicles by this much (roughly two hull radii plus margin).</summary>
    private const float TrafficSweepRadius = 4.8f;

    /// <summary>Smoothing may cut a corner that is up to this much slower than the planned path, as a ratio.</summary>
    private const float SmoothingSlack = 1.02f;

    private static readonly float DiagonalCost = Cell * MathF.Sqrt(2);

    private Obstacle[] staticGridFor = [];
    private bool[]? staticGrid;

    public IReadOnlyList<Obstacle> Obstacles { get; } = obstacles;

    public static bool Inside(Vector2 p, float radius = 0) =>
        Geometry.Finite(p) && Math.Abs(p.X) <= Mission.Edge - radius && Math.Abs(p.Y) <= Mission.Edge - radius;

    public bool Clear(Vector2 from, Vector2 to, float radius = 0) =>
        Inside(to, radius) && !Obstacles.Any(o => Geometry.HitBox(from, to, o, radius).HasValue);

    public List<Vector2> FindPath(Vector2 from, Vector2 requested, IReadOnlyList<Vehicle>? traffic = null, int self = -1)
    {
        if (!Geometry.Finite(from) || !Geometry.Finite(requested))
        {
            return [];
        }

        float goalLimit = Mission.Edge - GoalInset;
        var goal = Vector2.Clamp(requested, new(-goalLimit), new(goalLimit));
        float trafficCellRadiusSquared = TrafficCellRadius * TrafficCellRadius;

        bool TrafficFree(Vector2 p) =>
            traffic == null || !traffic.Any(t => t.Id != self && Vector2.DistanceSquared(p, t.Position) < trafficCellRadiusSquared);

        bool Free(Vector2 p) => Clear(p, p, Clearance) && TrafficFree(p);

        bool TrafficBlocks(Vehicle t, Vector2 a, Vector2 b)
        {
            if (t.Id == self)
            {
                return false;
            }

            // A stopped hull may already be inside the planning margin. Let it back
            // out of that margin, while the actual movement still enforces collision.
            if (Vector2.DistanceSquared(a, t.Position) < trafficCellRadiusSquared && Vector2.Dot(b - a, a - t.Position) > 0)
            {
                return false;
            }

            return Geometry.HitCircle(a, b, t.Position, TrafficSweepRadius).HasValue;
        }

        bool Segment(Vector2 a, Vector2 b) =>
            Clear(a, b, Mission.VehicleRadius) && (traffic == null || !traffic.Any(t => TrafficBlocks(t, a, b)));

        var staticFree = StaticGrid();
        int start = -1, end = -1;
        float bestStart = float.MaxValue, bestEnd = float.MaxValue;
        var free = new bool[Size * Size];
        for (int i = 0; i < free.Length; i++)
        {
            var p = Point(i);
            free[i] = staticFree[i] && TrafficFree(p);
            if (!free[i])
            {
                continue;
            }

            float s = Vector2.DistanceSquared(from, p);
            float e = Vector2.DistanceSquared(goal, p);
            if (s < bestStart && Segment(from, p))
            {
                bestStart = s;
                start = i;
            }
            if (e < bestEnd)
            {
                bestEnd = e;
                end = i;
            }
        }
        if (start < 0 || end < 0)
        {
            return [];
        }

        if (!Free(goal))
        {
            goal = Point(end);
        }

        if (terrain == null && Segment(from, goal))
        {
            return [goal];
        }

        var frontier = new PriorityQueue<int, float>();
        var previous = new int[free.Length];
        var cost = new float[free.Length];
        Array.Fill(cost, float.PositiveInfinity);
        Array.Fill(previous, -1);
        frontier.Enqueue(start, 0);
        cost[start] = 0;
        while (frontier.TryDequeue(out int current, out _))
        {
            if (current == end)
            {
                break;
            }

            int x = current % Size;
            int y = current / Size;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= Size || ny >= Size)
                    {
                        continue;
                    }

                    int next = ny * Size + nx;
                    if (!free[next] || !Segment(Point(current), Point(next)))
                    {
                        continue;
                    }

                    float step = dx != 0 && dy != 0 ? DiagonalCost : Cell;
                    if (terrain != null)
                    {
                        step *= (Slowness(Point(current)) + Slowness(Point(next))) / 2;
                    }

                    float newCost = cost[current] + step;
                    if (newCost >= cost[next])
                    {
                        continue;
                    }

                    cost[next] = newCost;
                    previous[next] = current;
                    frontier.Enqueue(next, newCost + Vector2.Distance(Point(next), goal));
                }
            }
        }
        if (start != end && previous[end] < 0)
        {
            return [];
        }

        List<Vector2> raw = [];
        for (int node = end; node != -1; node = previous[node])
        {
            raw.Add(Point(node));
        }

        raw.Reverse();
        if (Segment(raw[^1], goal))
        {
            raw.Add(goal);
        }

        // Smooth: from each anchor, skip ahead to the furthest point the whole hull can reach directly
        // (and, on Classic terrain, without a slower straight line replacing a faster road).
        List<Vector2> result = [];
        var anchor = from;
        for (int i = 0; i < raw.Count;)
        {
            int last = i;
            float planned = Travel(anchor, raw[i]);
            while (last + 1 < raw.Count && Segment(anchor, raw[last + 1]))
            {
                planned += Travel(raw[last], raw[last + 1]);
                if (terrain != null && Travel(anchor, raw[last + 1]) > planned * SmoothingSlack)
                {
                    break;
                }

                last++;
            }

            result.Add(raw[last]);
            anchor = raw[last];
            i = last + 1;
        }
        return result;
    }

    private float Slowness(Vector2 p) => terrain == null ? 1 : 1 / TerrainMap.Speed(terrain.At(p));

    private float Travel(Vector2 a, Vector2 b) => terrain?.TravelCost(a, b) ?? Vector2.Distance(a, b);

    private static Vector2 Point(int id) => new((id % Size) * Cell - Mission.Edge, (id / Size) * Cell - Mission.Edge);

    /// <summary>
    /// Walkability of every cell against the static obstacles. Obstacles never change during a
    /// mission, so this is computed once; it is rebuilt only if the obstacle list is edited (tests do).
    /// </summary>
    private bool[] StaticGrid()
    {
        if (staticGrid == null || !Obstacles.SequenceEqual(staticGridFor))
        {
            staticGridFor = [.. Obstacles];
            staticGrid = new bool[Size * Size];
            for (int i = 0; i < staticGrid.Length; i++)
            {
                var p = Point(i);
                staticGrid[i] = Clear(p, p, Clearance);
            }
        }
        return staticGrid;
    }
}
