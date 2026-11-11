namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// Classic-mode ground: a grid of <see cref="Terrain"/> cells on the reconnaissance grid (four-metre
/// cells). Roads are fast and exposed; jungle is slow, and its edge is the best place to fight from;
/// rivers can be forded anywhere, slowly. The layouts are Breakwater's own, drawn around each island's
/// cover and objectives.
/// </summary>
public sealed class TerrainMap
{
    public const int GridSize = Reconnaissance.GridSize;
    public const float CellSize = Reconnaissance.CellSize;

    // Jungle bands, measured inwards from a jungle's outline: edge, then fringe, then deep jungle.
    private const float EdgeDepth = CellSize;
    private const float FringeDepth = CellSize * 2;

    // A cell belongs to a road, track or river whose centre line passes this close to the cell's centre.
    private const float RoadHalfWidth = 3;
    private const float RiverHalfWidth = 4;

    /// <summary>Sampling step, in metres, when costing travel along a straight line.</summary>
    private const float TravelSample = 1;

    private readonly Terrain[] cells;

    private TerrainMap(Terrain[] cells) => this.cells = cells;

    public Terrain this[int index] => cells[index];

    /// <summary>Fraction of a vehicle's top speed it can reach on this ground.</summary>
    public static float Speed(Terrain terrain) => terrain switch
    {
        Terrain.Road => 1,
        Terrain.Track => .9f,
        Terrain.JungleTrack => .6f,
        Terrain.JungleEdge => .5f,
        Terrain.JungleFringe => .4f,
        Terrain.DeepJungle => .25f,
        Terrain.River => .125f,
        _ => .6f,
    };

    /// <summary>How many times wider enemy fire scatters around a target standing on this ground.</summary>
    public static float Cover(Terrain terrain) => terrain switch
    {
        Terrain.Road or Terrain.Track or Terrain.River => .5f,
        Terrain.JungleEdge => 16,
        Terrain.JungleFringe or Terrain.DeepJungle => 8,
        _ => 1,
    };

    /// <summary>Whether a vehicle here is hidden until the enemy is <see cref="ClassicRules.ConcealmentFactor"/> times closer.</summary>
    public static bool Conceals(Terrain terrain) => terrain is Terrain.DeepJungle or Terrain.JungleTrack;

    public static TerrainMap For(int island)
    {
        var layout = Layouts[island];
        var cells = new Terrain[GridSize * GridSize];
        for (int i = 0; i < cells.Length; i++)
        {
            var p = Reconnaissance.CellCenter(i);
            float inside = layout.Jungles.Count == 0 ? float.NegativeInfinity : layout.Jungles.Max(j => j.Radius - Vector2.Distance(p, j.Center));
            bool jungle = inside >= 0;
            if (Near(p, layout.River, RiverHalfWidth) && !Near(p, layout.Roads, RoadHalfWidth))
            {
                cells[i] = Terrain.River;
            }
            else if (Near(p, layout.Roads, RoadHalfWidth))
            {
                cells[i] = jungle ? Terrain.JungleTrack : Terrain.Road;
            }
            else if (Near(p, layout.Tracks, RoadHalfWidth))
            {
                cells[i] = jungle ? Terrain.JungleTrack : Terrain.Track;
            }
            else if (!jungle)
            {
                cells[i] = Terrain.Open;
            }
            else
            {
                cells[i] = inside < EdgeDepth ? Terrain.JungleEdge : inside < FringeDepth ? Terrain.JungleFringe : Terrain.DeepJungle;
            }
        }

        return new(cells);
    }

    /// <summary>The ground at <paramref name="p"/>. Points off the grid take the nearest cell.</summary>
    public Terrain At(Vector2 p)
    {
        float first = Reconnaissance.GridOrigin - CellSize / 2;
        int x = Math.Clamp((int)MathF.Floor((p.X - first) / CellSize), 0, GridSize - 1);
        int y = Math.Clamp((int)MathF.Floor((p.Y - first) / CellSize), 0, GridSize - 1);
        return cells[y * GridSize + x];
    }

    /// <summary>Time-equivalent length of the straight line from <paramref name="a"/> to <paramref name="b"/>: metres ÷ speed fraction.</summary>
    public float TravelCost(Vector2 a, Vector2 b)
    {
        float length = Vector2.Distance(a, b);
        int samples = Math.Max(1, (int)MathF.Ceiling(length / TravelSample));
        float cost = 0;
        for (int i = 0; i < samples; i++)
        {
            cost += 1 / Speed(At(Vector2.Lerp(a, b, (i + .5f) / samples)));
        }

        return cost * length / samples;
    }

    /// <summary>Test support: makes every cell <paramref name="terrain"/>.</summary>
    internal void Fill(Terrain terrain) => Array.Fill(cells, terrain);

    /// <summary>Test support: lays <paramref name="terrain"/> along the line <paramref name="a"/>–<paramref name="b"/>.</summary>
    internal void Paint(Vector2 a, Vector2 b, float halfWidth, Terrain terrain)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (Geometry.DistanceToSegment(Reconnaissance.CellCenter(i), a, b) <= halfWidth)
            {
                cells[i] = terrain;
            }
        }
    }

    private static bool Near(Vector2 p, IReadOnlyList<Vector2[]> lines, float halfWidth) =>
        lines.Any(line => Near(p, line, halfWidth));

    private static bool Near(Vector2 p, Vector2[] line, float halfWidth)
    {
        for (int i = 0; i + 1 < line.Length; i++)
        {
            if (Geometry.DistanceToSegment(p, line[i], line[i + 1]) <= halfWidth)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record Jungle(Vector2 Center, float Radius);

    private sealed record Layout(IReadOnlyList<Jungle> Jungles, IReadOnlyList<Vector2[]> Roads, IReadOnlyList<Vector2[]> Tracks, Vector2[] River);

    // One layout per island. Roads run from the landing beach towards the objectives; each island
    // has a river to ford and jungles placed so that their edges overlook the approaches.
    private static readonly Layout[] Layouts =
    [
        new(
            [new(new(-58, 40), 16), new(new(55, 16), 17), new(new(4, -33), 14), new(new(-30, -78), 12)],
            [
                [new(-20, 92), new(-10, 50), new(16, 34)],
                [new(16, 28), new(0, 5), new(-45, -20), new(-56, -30)],
                [new(22, 24), new(58, -2), new(58, -30), new(50, -50)],
            ],
            [[new(-60, 70), new(-78, 20), new(-66, -20)], [new(-45, -20), new(-20, -45), new(30, -70)]],
            [new(-92, 55), new(-45, 52), new(-20, 56), new(10, 52), new(40, 58), new(92, 54)]),
        new(
            [new(new(25, 30), 16), new(new(-72, 5), 12), new(new(42, -60), 18), new(new(-18, -40), 12)],
            [
                [new(0, 92), new(0, 55), new(-38, 35)],
                [new(0, 55), new(35, 15), new(45, -12)],
                [new(-38, 30), new(-30, 0), new(-25, -40), new(-38, -56)],
            ],
            [[new(45, -12), new(70, -40), new(60, -80)]],
            [new(92, 8), new(60, 5), new(30, 0), new(22, -10)]),
        new(
            [new(new(-60, 20), 16), new(new(20, 42), 12), new(new(32, -40), 13), new(new(-40, -77), 12)],
            [
                [new(10, 92), new(10, 50), new(38, 32)],
                [new(10, 50), new(-35, 10), new(-44, -14)],
                [new(38, 24), new(35, 5), new(30, -20), new(10, -50), new(8, -58)],
            ],
            [[new(-44, -14), new(-70, -40), new(-60, -85)]],
            [new(92, 60), new(60, 55), new(35, 62), new(20, 92)]),
    ];
}
