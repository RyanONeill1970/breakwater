namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class CombatTests
{
    [Fact]
    public void Turret_alignment_and_line_of_sight_gate_firing()
    {
        var s = Empty();
        var a = Tank(0, new(0, 20));
        var b = Tank(1, new(0, -20), true);
        s.Vehicles.AddRange([a, b]);
        a.TurretHeading = MathF.PI;
        Assert.False(s.TryFire(a, b), "Fired backwards");
        a.TurretHeading = 0;
        s.Obstacles.Add(new(Vector2.Zero, new(5, 2), 4, ObstacleKind.Wall));
        Assert.False(s.TryFire(a, b), "Fired through cover");
        s.Obstacles.Clear();
        Assert.True(s.TryFire(a, b), "Valid shot rejected");
        Assert.Equal(Vehicle.MaxHealth, b.Health);
        Run(s, 1);
        Assert.True(b.Health < Vehicle.MaxHealth, "Shell missed stationary target");
    }

    [Fact]
    public void Incoming_projectiles_hit_cover_before_vehicles()
    {
        var s = Empty();
        var a = Tank(0, new(0, 20));
        var b = Tank(1, new(0, -20), true);
        s.Vehicles.AddRange([a, b]);
        Assert.True(s.TryFire(a, b));
        s.Obstacles.Add(new(Vector2.Zero, new(5, .1f), 4, ObstacleKind.Wall));
        Run(s, 1);
        Assert.Equal(Vehicle.MaxHealth, b.Health);
        Assert.Empty(s.Shells);
    }

    [Fact]
    public void Frontal_armor_absorbs_more_than_rear_armor()
    {
        var t = Tank(0, Vector2.Zero);
        Assert.True(Simulation.ArmorDamage(t, new(0, 1), 40) < Simulation.ArmorDamage(t, new(0, -1), 40));
    }

    [Fact]
    public void Hits_report_the_armor_face_they_struck()
    {
        var t = Tank(0, Vector2.Zero);
        Assert.Equal(ArmorFacing.Front, Simulation.Facing(t, new(0, 1)));
        Assert.Equal(ArmorFacing.Side, Simulation.Facing(t, new(1, 0)));
        Assert.Equal(ArmorFacing.Rear, Simulation.Facing(t, new(0, -1)));

        var s = Empty();
        var victim = Tank(18, new(0, 0), true);
        s.Vehicles.AddRange([Tank(0, new(0, 40)), victim]);
        s.Shells.Add(new Shell { Id = 0, Damage = 10, Direction = new(1, 0), Owner = 17, Position = new(-4, 0) });
        s.Step(.05f);
        Assert.Equal(ArmorFacing.Side, s.Events.First(e => e.Kind == CombatEventKind.Impact).Armor);
    }

    [Fact]
    public void Hold_fire_reload_and_ammunition_are_enforced()
    {
        var s = Empty();
        var a = Tank(0, new(0, 20));
        var b = Tank(1, new(0, -20), true);
        s.Vehicles.AddRange([a, b]);
        Run(s, 1);
        Assert.Equal(0, s.ShotsFired);
        a.Ammo = 1;
        Assert.True(s.TryFire(a, b), "First shot rejected");
        Assert.False(s.TryFire(a, b), "Reload ignored");
        a.Reload = 0;
        Assert.False(s.TryFire(a, b), "Fired without ammunition");
    }

    [Fact]
    public void Fog_suppresses_hidden_combat_events()
    {
        var s = Empty();
        var a = Tank(0, new(-80, 80));
        var b = Tank(17, new(40, -50), true);
        var victim = Tank(18, new(40, -80), true);
        s.Vehicles.AddRange([a, b, victim]);
        victim.Health = 0;
        // Impact a remote wreck beyond every living friendly observer.
        s.Shells.Add(new Shell { Id = 0, Damage = 40, Direction = new(0, -1), Owner = 17, Position = new(40, -76) });
        s.Step(.05f);
        var hit = s.Events.First(e => e.Kind == CombatEventKind.Impact);
        Assert.False(hit.PlayerVisible, "Hidden impact leaked through effects");
    }

    [Fact]
    public void Vehicle_specs_are_shared_instances()
    {
        Assert.Same(VehicleSpec.For(VehicleKind.Scout), VehicleSpec.For(VehicleKind.Scout));
    }
}
