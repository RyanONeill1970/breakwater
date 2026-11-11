namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class MissionTests
{
    [Fact]
    public void Mission_spawn_locations_are_valid_and_separated()
    {
        AssertNoOverlapsOrPenetration(new Simulation());
    }

    [Fact]
    public void All_island_deployments_and_objective_routes_are_valid()
    {
        for (int island = 0; island < Mission.Count; island++)
        {
            var s = new Simulation(new Campaign { Island = island });
            AssertNoOverlapsOrPenetration(s);
            foreach (var objective in s.Sites)
            {
                Assert.NotEmpty(s.Navigation.FindPath(s.Vehicles[0].Position, objective.Position));
            }
        }
    }

    [Fact]
    public void Capture_is_contested_reversible_and_leads_to_victory()
    {
        var s = Empty();
        s.Sites[0].Position = Vector2.Zero;
        var a = Tank(0, new(-5, 0));
        var b = Tank(1, new(5, 0), true);
        s.Vehicles.AddRange([a, b]);
        Run(s, 4);
        Assert.Equal(0, s.Sites[0].Progress);
        Assert.True(s.Sites[0].Contested);

        b.Position = new(60, 0);
        Run(s, 6);
        float progress = s.Sites[0].Progress;
        Assert.True(progress > 5, "Capture failed");

        a.Position = new(-60, 0);
        b.Position = new(5, 0);
        Run(s, 2);
        Assert.True(s.Sites[0].Progress < progress, "Capture did not reverse");

        a.Position = new(-5, 0);
        b.Health = 0;
        Run(s, CaptureSite.HoldSeconds + 2);
        Assert.Equal(MissionPhase.Victory, s.Phase);
    }

    [Fact]
    public void All_friendly_losses_produce_defeat()
    {
        var s = Empty();
        s.Vehicles.Add(Tank(0, Vector2.Zero));
        s.Vehicles[0].Health = 0;
        s.Step(.02f);
        Assert.Equal(MissionPhase.Defeat, s.Phase);
    }

    [Fact]
    public void Beach_supply_replenishes_ammunition_and_repairs_stationary_vehicles()
    {
        var s = Empty();
        var t = Tank(0, new(0, 75));
        t.Health = 40;
        t.Ammo = 0;
        s.Vehicles.Add(t);
        Run(s, 5);
        Assert.True(t.Health > 48 && t.Ammo >= 9, $"Supply not replenished: {t.Health} health, {t.Ammo} rounds");
    }

    [Fact]
    public void Four_groups_can_complete_the_mission_without_overlaps()
    {
        var s = new Simulation();
        s.Begin();
        AssaultAllSites(s, 300, () => AssertNoOverlapsOrPenetration(s));
        Assert.True(s.Phase == MissionPhase.Victory,
            $"Mission not completed: {s.Phase}, sites {string.Join(",", s.Sites.Select(o => o.Progress))}; " +
            string.Join(";", s.Vehicles.Where(v => v.Alive && !v.Enemy).Select(v => $"{v.Id}:{v.Position}:{v.Status}:{v.Orders.Count}")));
    }
}
