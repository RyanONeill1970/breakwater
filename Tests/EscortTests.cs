namespace Breakwater.Tests;

using static TestWorld;

public class EscortTests
{
    private const int EscortBattle = 1;

    [Fact]
    public void Supply_trucks_wait_then_drive_through_and_win_the_battle()
    {
        var s = new Simulation(ClassicAt(2, EscortBattle));
        var convoy = s.ZoneBattle!.Convoy!;
        Assert.Equal(convoy.Trucks, s.Trucks.Count());
        Assert.All(s.Trucks, t => Assert.False(t.Player));
        Assert.DoesNotContain(s.Trucks, t => Enumerable.Range(0, Mission.GroupCount).Any(g => s.Group(g).Contains(t)));

        s.Begin();
        foreach (var enemy in s.Vehicles.Where(v => v.Enemy))
        {
            enemy.Health = 0;
        }

        Run(s, convoy.DepartsAt - 1);
        Assert.All(s.Trucks, t => Assert.Empty(t.Orders));
        Assert.False(s.ConvoyMoving);

        for (int t = 0; t < 240 && s.Phase == MissionPhase.Active; t++)
        {
            Run(s, 1);
        }

        Assert.Equal(MissionPhase.Victory, s.Phase);
        Assert.Equal(convoy.Trucks, s.Delivered);
    }

    [Fact]
    public void Losing_too_many_trucks_fails_the_escort_but_they_are_not_player_losses()
    {
        var s = new Simulation(ClassicAt(2, EscortBattle));
        s.Begin();
        foreach (var truck in s.Trucks.Take(2))
        {
            truck.Health = 0;
        }

        Assert.Equal(0, s.Losses);
        s.Step(.1f);
        Assert.Equal(MissionPhase.Defeat, s.Phase);
        Assert.NotNull(s.BrokenCondition());
    }

    [Fact]
    public void Enemy_gunners_pick_out_the_trucks_first()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(-6, 20));
        var truck = Unit(Mission.FirstConvoyId, VehicleKind.Truck, new(6, 20));
        truck.Group = Mission.ConvoyGroup;
        var t62 = Unit(20, VehicleKind.T62, new(0, -20), true);
        s.Vehicles.AddRange([m1, truck, t62]);
        s.Recon.Refresh();
        Assert.Same(truck, s.Target(t62));
    }

    [Fact]
    public void Saves_keep_the_convoy_on_its_way()
    {
        var s = new Simulation(ClassicAt(2, EscortBattle));
        s.Begin();
        s.Elapsed = s.ZoneBattle!.Convoy!.DepartsAt - .01f;
        s.Step(.1f);
        Assert.All(s.Trucks, t => Assert.NotEmpty(t.Orders));

        var loaded = SaveGame.Read(SaveGame.Write(s));
        Assert.All(loaded.Trucks, t => Assert.NotEmpty(t.Orders));
        Assert.Equal(s.Trucks.Select(t => t.Id), loaded.Trucks.Select(t => t.Id));
    }
}
