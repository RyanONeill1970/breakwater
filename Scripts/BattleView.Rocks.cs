namespace Breakwater;

public partial class BattleView
{
    private readonly List<(Obstacle Obstacle, Node3D Scan)> ridgeScans = [];
    /// <summary>Complete scanned outcrops fitted inside the obstacle's blocking box, including rotation.</summary>
    private void BuildRidgeScans(Node3D node, Obstacle obstacle)
    {
        var random = new Random(431 + (int)obstacle.Center.X * 31 + (int)obstacle.Center.Y);
        bool alongX = obstacle.HalfSize.X > obstacle.HalfSize.Y;
        float major = (alongX ? obstacle.HalfSize.X : obstacle.HalfSize.Y) * 2;
        float minor = (alongX ? obstacle.HalfSize.Y : obstacle.HalfSize.X) * 2;
        int count = Math.Max(2, (int)Math.Ceiling(major / 7));
        float step = major / count;
        for (int i = 0; i < count; i++)
        {
            float width = Math.Min(major, step * 1.65f);
            float along = Math.Clamp(-major / 2 + step * (i + .5f), -major / 2 + width / 2, major / 2 - width / 2);
            float height = obstacle.Height * (.78f + random.NextSingle() * .22f);
            var size = alongX ? new Vector3(width, height + .2f, minor * .96f) : new(minor * .96f, height + .2f, width);
            var at = World(obstacle.Center, -.2f) + (alongX ? Vector3.Right : Vector3.Back) * along;
            var scan = islandAssets.Place(node, IslandAssets.Boulder, at, size, random.NextSingle() * 360, fitBox: true);
            ridgeScans.Add((obstacle, scan));
        }
        // Low plants on the rock shelf, all rooted inside cover rather than across a route.
        for (int i = 0; i < count + 2; i++)
        {
            float along = (random.NextSingle() - .5f) * major * .8f;
            float across = (i % 2 == 0 ? 1 : -1) * minor * .38f;
            var at = World(obstacle.Center, obstacle.Height * .35f) + (alongX ? new Vector3(along, 0, across) : new(across, 0, along));
            islandAssets.Place(node, IslandAssets.Fern, at, new(2.8f, 1.5f, 2.8f), random.NextSingle() * 360);
        }
    }

    /// <summary>Engine QA: actual imported mesh bounds must respect cover, and playable ground stays flat.</summary>
    internal void ValidateIslandGeometry()
    {
        const float tolerance = .02f;
        for (float x = -Mission.Edge; x <= Mission.Edge; x += 2)
        {
            for (float z = -Mission.Edge; z <= Mission.Edge; z += 2)
            {
                if (Math.Abs(IslandHeight(new(x, z))) > tolerance)
                {
                    throw new InvalidOperationException($"Playable ground moved at {x}, {z}.");
                }
            }
        }
        foreach (var (obstacle, scan) in ridgeScans)
        {
            foreach (var mesh in scan.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                var bounds = GlobalTransform.AffineInverse() * mesh.GlobalTransform * mesh.GetAabb();
                var min = obstacle.Center - obstacle.HalfSize;
                var max = obstacle.Center + obstacle.HalfSize;
                if (bounds.Position.X < min.X - tolerance || bounds.End.X > max.X + tolerance ||
                    bounds.Position.Z < min.Y - tolerance || bounds.End.Z > max.Y + tolerance || bounds.End.Y > obstacle.Height + tolerance)
                {
                    throw new InvalidOperationException($"Rock extends beyond blocking cover at {obstacle.Center}: {bounds}.");
                }
            }
        }
    }
}
