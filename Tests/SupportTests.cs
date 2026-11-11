namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class SupportTests
{
    [Fact]
    public void Smoke_blocks_sight_until_it_clears()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 20));
        var t62 = Unit(20, VehicleKind.T62, new(0, -20), true);
        s.Vehicles.AddRange([m1, t62]);
        s.Recon.Refresh();
        Assert.True(s.Recon.Spotted(t62));

        Assert.Equal(1, s.FireSmoke(0));
        Assert.Equal(m1.Spec.SmokeRounds - 1, m1.Smoke);
        Assert.False(s.Recon.Spotted(t62), "Seen through smoke");
        Assert.False(s.Recon.Spotted(m1), "The enemy saw through smoke");

        Run(s, Simulation.SmokeSeconds + 1);
        Assert.Empty(s.Smoke);
        Assert.True(s.Recon.Spotted(t62), "The smoke never cleared");

        Assert.Equal(1, s.FireSmoke(0));
        Assert.Equal(0, s.FireSmoke(0));
    }

    [Fact]
    public void Artillery_lands_after_a_delay_and_hurts_whoever_is_under_it()
    {
        var s = EmptyClassic();
        var btr = Unit(20, VehicleKind.Btr, new(0, -40), true);
        s.Vehicles.AddRange([Unit(0, VehicleKind.M113, new(60, 60)), btr]);

        Assert.True(s.CallArtillery(btr.Position, ArtilleryShell.Cluster));
        Assert.Equal(Simulation.PlayerArtilleryMissions - 1, s.ArtilleryLeft);
        Run(s, Simulation.ArtilleryDelay - 1);
        Assert.Equal(Vehicle.MaxHealth, btr.Health);

        Run(s, 5);
        Assert.True(btr.Health < Vehicle.MaxHealth, "The barrage missed");
        Assert.Empty(s.Strikes);

        Assert.True(s.CallArtillery(Vector2.Zero, ArtilleryShell.HighExplosive));
        Assert.True(s.CallArtillery(Vector2.Zero, ArtilleryShell.Smoke));
        Assert.False(s.CallArtillery(Vector2.Zero, ArtilleryShell.HighExplosive), "Called more missions than the battery has");
    }

    [Fact]
    public void Mines_arm_then_wreck_whoever_drives_over_them()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 20));
        s.Vehicles.Add(m1);
        Assert.True(s.LayMine(0));
        Assert.Equal(Simulation.PlayerMines - 1, s.MinesLeft);
        var mine = s.Mines.Single();
        Assert.False(mine.Enemy);

        var btr = Unit(20, VehicleKind.Btr, mine.Position, true);
        s.Vehicles.Add(btr);
        Run(s, 1);
        Assert.Equal(Vehicle.MaxHealth, btr.Health);

        Run(s, Simulation.MineArmSeconds);
        Assert.Empty(s.Mines);
        Assert.False(btr.Alive, "A mine should wreck a carrier");
    }

    [Fact]
    public void Enemy_guns_fire_on_reported_player_vehicles_with_a_warning()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 20));
        s.Vehicles.AddRange([m1, Unit(20, VehicleKind.T62, new(0, -20), true)]);
        s.EnemyArtilleryLeft = 1;
        s.NextEnemyArtillery = 0;
        Run(s, .5f);

        var strike = s.Strikes.Single();
        Assert.True(strike.Enemy);
        Assert.Equal(m1.Position, strike.Target);
        Assert.Contains(s.Events, e => e.Kind == CombatEventKind.IncomingArtillery && e.Message.Length > 0);
        Assert.Equal(0, s.EnemyArtilleryLeft);
    }

    [Fact]
    public void Classic_islands_start_with_enemy_minefields_and_modern_ones_without()
    {
        for (int island = 0; island < Mission.Count; island++)
        {
            var s = new Simulation(ClassicAt(island));
            Assert.Equal(Mission.MinesPerField, s.Mines.Count(m => m.Enemy));
            Assert.All(s.Mines, m => Assert.True(s.Navigation.Clear(m.Position, m.Position), "A mine inside cover"));
            Assert.Equal(Simulation.PlayerArtilleryMissions, s.ArtilleryLeft);
        }

        var training = new Simulation(Campaign.Start(GameMode.Classic));
        Assert.True(training.ZoneBattle!.Training);
        Assert.Empty(training.Mines);
        Assert.Equal(0, training.EnemyArtilleryLeft);

        var modern = new Simulation();
        Assert.Empty(modern.Mines);
        Assert.Equal(0, modern.ArtilleryLeft);
    }
}
