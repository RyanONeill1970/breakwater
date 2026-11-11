namespace Breakwater.Tests;

using System.Numerics;

public class NavigationTests
{
    [Fact]
    public void Routes_detour_around_cover_with_full_hull_clearance()
    {
        var nav = new Navigation([new Obstacle(Vector2.Zero, new(5, 20), 6, ObstacleKind.Ridge)]);
        var start = new Vector2(-25, 0);
        var route = nav.FindPath(start, new(25, 0));
        Assert.True(route.Count > 1, "Expected a detour");
        foreach (var point in route)
        {
            Assert.True(nav.Clear(start, point, Mission.VehicleRadius), "Route clips cover");
            start = point;
        }
        Assert.True(Vector2.Distance(start, new(25, 0)) < 1, "Wrong destination");
    }

    [Fact]
    public void Blocked_destinations_resolve_to_open_ground_and_disconnected_routes_fail()
    {
        var nav = new Navigation([new Obstacle(Vector2.Zero, new(5, 20), 6, ObstacleKind.Ridge)]);
        var route = nav.FindPath(new(-25, 0), Vector2.Zero);
        Assert.NotEmpty(route);
        Assert.True(nav.Clear(route[^1], route[^1], Mission.VehicleRadius), "Invalid fallback");

        var wall = new Navigation([new Obstacle(Vector2.Zero, new(5, 100), 6, ObstacleKind.Ridge)]);
        Assert.Empty(wall.FindPath(new(-25, 0), new(25, 0)));
    }

    [Fact]
    public void Path_grid_follows_obstacles_added_after_construction()
    {
        List<Obstacle> obstacles = [];
        var nav = new Navigation(obstacles);
        Assert.Single(nav.FindPath(new(-25, 0), new(25, 0)));
        obstacles.Add(new(Vector2.Zero, new(5, 20), 6, ObstacleKind.Ridge));
        Assert.True(nav.FindPath(new(-25, 0), new(25, 0)).Count > 1, "Cached grid ignored new cover");
    }

    [Fact]
    public void Swept_shell_intersection_catches_a_thin_wall()
    {
        var hit = Geometry.HitBox(new(-20, 0), new(20, 0), new(Vector2.Zero, new(.1f, 5), 4, ObstacleKind.Wall));
        Assert.True(hit is > .49f and < .51f, "Missed wall");
    }
}
