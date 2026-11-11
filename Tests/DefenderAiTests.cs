namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class DefenderAiTests
{
    [Fact]
    public void Defenders_do_not_chase_unobserved_units_behind_cover()
    {
        var s = Empty();
        var a = Tank(0, new(20, 0));
        var b = Tank(17, new(-20, 0), true);
        b.Think = 0;
        s.Vehicles.AddRange([a, b]);
        s.Obstacles.Add(new(Vector2.Zero, new(2, 40), 5, ObstacleKind.Ridge));
        Run(s, 2);
        Assert.Empty(s.Recon.EnemyIntel);
        Assert.Equal(Tactic.Guard, b.Tactic);
        Assert.Empty(b.Route);
    }

    [Fact]
    public void Defenders_flank_observed_contacts_and_retreat_when_damaged()
    {
        var s = Empty();
        var a = Tank(0, new(0, 40));
        var b = Tank(17, Vector2.Zero, true);
        b.Think = 0;
        s.Vehicles.AddRange([a, b]);
        s.Step(.02f);
        Assert.Equal(Tactic.Flank, b.Tactic);
        Assert.NotEmpty(b.Route);

        b.Health = 20;
        b.Think = 0;
        s.Step(.02f);
        Assert.Equal(Tactic.Retreat, b.Tactic);
    }

    [Fact]
    public void Idle_defenders_patrol()
    {
        var s = Empty();
        var a = Tank(0, new(80, 80));
        var b = Tank(16, Vector2.Zero, true);
        b.Think = 0;
        s.Vehicles.AddRange([a, b]);
        s.Step(.02f);
        Assert.Equal(Tactic.Patrol, b.Tactic);
        Assert.NotEmpty(b.Route);
    }
}
