namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class MovementTests
{
    [Fact]
    public void Manual_drive_cannot_pass_through_cover()
    {
        var s = Empty();
        var t = Tank(0, new(0, 10));
        s.Vehicles.Add(t);
        s.Obstacles.Add(new(Vector2.Zero, new(5, 1), 4, ObstacleKind.Wall));
        Run(s, 10, manualId: 0, drive: 1);
        Assert.True(t.Position.Y >= 3.1f, "Drove through wall");
    }

    [Fact]
    public void Manual_drive_cannot_pass_through_another_vehicle()
    {
        var s = Empty();
        var t = Tank(0, new(0, 10));
        s.Vehicles.AddRange([t, Tank(1, Vector2.Zero)]);
        Run(s, 10, manualId: 0, drive: 1);
        Assert.True(t.Position.Y >= Mission.VehicleRadius * 2, "Drove through vehicle");
    }

    [Fact]
    public void Paused_simulation_does_not_move_reload_or_capture()
    {
        var s = Empty();
        var t = Tank(0, new(0, 20));
        s.Vehicles.Add(t);
        t.Reload = 2;
        s.Paused = true;
        s.Step(.2f, 0, 1, 0);
        Assert.Equal(0, s.Elapsed);
        Assert.Equal(2, t.Reload);
        Assert.Equal(20, t.Position.Y);
    }

    [Fact]
    public void A_vehicle_follows_a_detour_and_completes_queued_orders()
    {
        var s = Empty();
        s.Obstacles.Add(new(Vector2.Zero, new(5, 15), 5, ObstacleKind.Ridge));
        var t = Tank(0, new(-25, 0));
        s.Vehicles.Add(t);
        Assert.Equal(1, s.Order(0, new(30, 0)));
        s.Order(0, new(35, 30), true);
        for (int i = 0; i < 60 * 80; i++)
        {
            s.Step(1f / 60);
            Assert.True(s.Navigation.Clear(t.Position, t.Position, Mission.VehicleRadius), "Vehicle entered obstacle");
        }
        Assert.Empty(t.Orders);
        Assert.True(Vector2.Distance(t.Position, new(31.5f, 26.5f)) < 2, $"Did not arrive: {t.Position}, {t.Status}");
    }

    [Fact]
    public void An_unreachable_queued_order_is_dropped_instead_of_freezing_the_vehicle()
    {
        var s = Empty();
        // Box the tank in, so nothing outside can be reached.
        s.Obstacles.AddRange(
        [
            new(new(0, 10), new(10, 1), 4, ObstacleKind.Wall),
            new(new(0, -10), new(10, 1), 4, ObstacleKind.Wall),
            new(new(10, 0), new(1, 10), 4, ObstacleKind.Wall),
            new(new(-10, 0), new(1, 10), 4, ObstacleKind.Wall),
        ]);
        var t = Tank(0, Vector2.Zero);
        s.Vehicles.Add(t);
        t.Orders.Add(new(50, 50));
        t.Status = VehicleStatus.Moving;

        s.Step(1f / 60);

        Assert.Empty(t.Orders);
        Assert.Equal(VehicleStatus.NoRoute, t.Status);
    }

    [Fact]
    public void Traffic_rerouting_avoids_a_stationary_vehicle()
    {
        var s = Empty();
        var t = Tank(0, new(0, 25));
        var blocker = Tank(1, Vector2.Zero);
        blocker.Group = 1;
        s.Vehicles.AddRange([t, blocker]);
        s.Order(0, new(3.5f, -25));
        for (int i = 0; i < 60 * 40; i++)
        {
            s.Step(1f / 60);
            Assert.True(Vector2.Distance(t.Position, blocker.Position) >= 4.19f, "Traffic overlap");
        }
        Assert.True(t.Orders.Count == 0, $"Vehicle stuck: {t.Position} / {t.Status}");
    }
}
