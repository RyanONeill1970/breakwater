namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class ClassicTests
{
    [Fact]
    public void Damage_is_weapon_power_over_armour_scaled_by_the_dice()
    {
        float tow = WeaponSpec.For(Weapon.Tow).Power;
        float sabot = WeaponSpec.For(Weapon.Sabot).Power;
        float mg = WeaponSpec.For(Weapon.MachineGun).Power;
        int m1 = VehicleSpec.For(VehicleKind.Abrams).Armor;
        int btr = VehicleSpec.For(VehicleKind.Btr).Armor;

        Assert.Equal(Vehicle.MaxHealth * .5f * .25f, ClassicRules.Damage(sabot, m1, .25f), .001f);
        Assert.True(ClassicRules.Damage(tow, btr, ClassicRules.MinimumDamageRoll) >= Vehicle.MaxHealth, "A missile should wreck a carrier outright");
        Assert.True(ClassicRules.Damage(mg, m1, 1) < .1f, "A machine gun should barely scratch an M1");
    }

    [Fact]
    public void Vehicles_choose_ammunition_by_target_range_and_halt()
    {
        var m1 = Unit(0, VehicleKind.Abrams, Vector2.Zero);
        Assert.Equal(Weapon.Sabot, Simulation.ChooseWeapon(m1, Unit(1, VehicleKind.T72, new(0, -70), true)));
        Assert.Equal(Weapon.Heat, Simulation.ChooseWeapon(m1, Unit(1, VehicleKind.Btr, new(0, -40), true)));
        Assert.Null(Simulation.ChooseWeapon(m1, Unit(1, VehicleKind.T72, new(0, -90), true)));

        m1.Rounds[(int)Weapon.Heat] = 0;
        Assert.Equal(Weapon.MachineGun, Simulation.ChooseWeapon(m1, Unit(1, VehicleKind.Btr, new(0, -10), true)));
        Assert.Null(Simulation.ChooseWeapon(m1, Unit(1, VehicleKind.Btr, new(0, -40), true)));

        var itv = Unit(2, VehicleKind.Itv, Vector2.Zero);
        var t72 = Unit(3, VehicleKind.T72, new(0, -100), true);
        Assert.Equal(Weapon.Tow, Simulation.ChooseWeapon(itv, t72));
        itv.Speed = 3;
        Assert.Null(Simulation.ChooseWeapon(itv, t72));
        Assert.Equal(Weapon.Tow, Simulation.ChooseWeapon(itv, t72, anyMotion: true));
    }

    [Fact]
    public void Switching_ammunition_costs_a_reload_and_firing_spends_a_round_and_reveals_the_shooter()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 20));
        var t72 = Unit(1, VehicleKind.T72, new(0, -20), true);
        s.Vehicles.AddRange([m1, t72]);
        s.Recon.Refresh();
        Assert.Equal(Weapon.Sabot, m1.Loaded);

        m1.Loaded = Weapon.Heat;
        Assert.False(s.TryFire(m1, t72), "Fired HEAT at a tank instead of loading sabot");
        Assert.Equal(Weapon.Sabot, m1.Loaded);
        Assert.Equal(WeaponSpec.For(Weapon.Sabot).Reload, m1.Reload);

        Run(s, WeaponSpec.For(Weapon.Sabot).Reload);
        Assert.True(s.TryFire(m1, t72));
        Assert.Equal(VehicleSpec.For(VehicleKind.Abrams).Magazine[(int)Weapon.Sabot] - 1, m1.Rounds[(int)Weapon.Sabot]);
        Assert.Equal(WeaponSpec.For(Weapon.Sabot).Reload, m1.Reload);
        Assert.Equal(ClassicRules.RevealSeconds, m1.Revealed);
        Assert.Equal(WeaponSpec.For(Weapon.Sabot).Range, s.Shells.Single().Remaining);
    }

    [Fact]
    public void Jungle_edge_cover_spoils_the_enemy_aim()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 40));
        var t62 = Unit(1, VehicleKind.T62, new(0, -20), true);
        s.Vehicles.AddRange([m1, t62]);
        s.Recon.Refresh();
        float open = s.Spread(m1, t62);

        s.Terrain!.Paint(t62.Position, t62.Position, 3, Terrain.JungleEdge);
        Assert.Equal(open * 16, s.Spread(m1, t62), .0001f);
        s.Terrain.Paint(t62.Position, t62.Position, 3, Terrain.Road);
        Assert.Equal(open / 2, s.Spread(m1, t62), .0001f);
        m1.Speed = 5;
        Assert.Equal(open, s.Spread(m1, t62), .0001f);
        m1.Speed = 0;

        Assert.True(HitsOutOf(s, m1, t62, 60, Terrain.Open) > 50, "Too many misses against a target in the open");
        Assert.True(HitsOutOf(s, m1, t62, 60, Terrain.JungleEdge) < 25, "Too many hits against a target at the jungle edge");

        // The player's crews firing on their own miss far more than a gun aimed through the sight.
        Assert.True(HitsOutOf(s, m1, t62, 60, Terrain.Open, aimed: false) < 50, "Automatic fire is as good as aiming");
    }

    [Fact]
    public void A_vehicle_in_deep_jungle_stays_hidden_until_it_fires()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 20));
        var t62 = Unit(1, VehicleKind.T62, new(0, -20), true);
        s.Vehicles.AddRange([m1, t62]);
        s.Terrain!.Paint(t62.Position, t62.Position, 3, Terrain.DeepJungle);

        s.Recon.Refresh();
        Assert.False(s.Recon.Spotted(t62), "Seen in deep jungle from 40 m");

        t62.Revealed = ClassicRules.RevealSeconds;
        s.Recon.Refresh();
        Assert.True(s.Recon.Spotted(t62), "Firing did not give the position away");

        Run(s, ClassicRules.RevealSeconds + 1);
        Assert.False(s.Recon.Spotted(t62), "Stayed revealed after the muzzle flash was forgotten");

        m1.Position = new(0, -12);
        s.Recon.Refresh();
        Assert.True(s.Recon.Spotted(t62), "Not seen from close range");
    }

    [Fact]
    public void Terrain_sets_top_speed_and_routes_follow_the_faster_road()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 40));
        s.Vehicles.Add(m1);
        s.Terrain!.Fill(Terrain.Road);
        Run(s, 2, m1.Id, 1);
        Assert.Equal(m1.Spec.Speed, m1.Speed, .01f);

        s.Terrain.Fill(Terrain.River);
        Run(s, 2, m1.Id, 1);
        Assert.Equal(m1.Spec.Speed * TerrainMap.Speed(Terrain.River), m1.Speed, .01f);

        // Straight through jungle is 80 m at a quarter speed; the road detour is twice as long at full speed.
        s.Terrain.Fill(Terrain.DeepJungle);
        Vector2[] road = [new(0, 40), new(40, 40), new(40, -40), new(0, -40)];
        for (int i = 0; i + 1 < road.Length; i++)
        {
            s.Terrain.Paint(road[i], road[i + 1], 3, Terrain.Road);
        }

        var route = s.Navigation.FindPath(new(0, 40), new(0, -40));
        Assert.Contains(route, p => p.X > 30);
    }

    [Fact]
    public void A_classic_campaign_fields_the_classic_forces()
    {
        var c = Campaign.Start(GameMode.Classic);
        Assert.True(c.Valid());
        Assert.Equal([VehicleKind.Abrams, VehicleKind.Abrams, VehicleKind.Itv, VehicleKind.Itv], c.Roster.Take(4).Select(r => r.Kind));
        Assert.Equal([VehicleKind.Bradley, VehicleKind.Bradley, VehicleKind.M113, VehicleKind.M113], c.Roster.Skip(12).Select(r => r.Kind));
        Assert.Equal(Loadout.Mixed, c.LoadoutOf(0));
        Assert.Equal(Loadout.Recon, c.LoadoutOf(3));

        Assert.True(c.Refit(0));
        Assert.Equal(Loadout.Recon, c.LoadoutOf(0));
        Assert.Equal(VehicleKind.Bradley, c.Roster[0].Kind);

        var s = new Simulation(ClassicAt(0));
        Assert.NotNull(s.Terrain);
        Assert.Equal(0, s.ReinforcementWaves);
        VehicleKind[] garrison = [VehicleKind.T62, VehicleKind.Bmp, VehicleKind.Btr];
        Assert.All(s.Vehicles.Where(v => v.Enemy), v => Assert.Contains(v.Kind, garrison));
        Assert.All(s.Vehicles, v => Assert.Equal(v.Spec.Magazine, v.Rounds));

        Assert.Null(new Simulation().Terrain);
        c.Roster[5].Kind = VehicleKind.MainTank;
        Assert.False(c.Valid(), "A Modern vehicle in a Classic roster");
    }

    [Fact]
    public void Classic_defenders_stand_their_ground_and_return_to_their_posts()
    {
        var s = EmptyClassic();
        var home = new Vector2(0, -30);
        var t62 = Unit(20, VehicleKind.T62, home, true);
        t62.Think = 0;
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 30));
        s.Vehicles.AddRange([t62, m1]);
        Run(s, 15);
        Assert.True(Vector2.Distance(t62.Position, home) < 1, "A Classic defender left its post to hunt");

        // Out of contact range, and the old report long stale.
        m1.Position = new(80, 80);
        s.Elapsed += 60;
        t62.Position = new(20, -30);
        t62.Think = 0;
        Run(s, 20);
        Assert.True(Vector2.Distance(t62.Position, home) < 3, "The defender did not return to its post");
    }

    [Fact]
    public void A_classic_island_can_be_fought_without_overlaps()
    {
        var s = new Simulation(ClassicAt(0));
        s.Begin();
        AssaultAllSites(s, 240);
        AssertNoOverlapsOrPenetration(s);
        Assert.Contains(s.Vehicles, v => v.Enemy && !v.Alive);
        Assert.True(s.ShotsFired > 0);
    }

    [Fact]
    public void The_gunner_lays_the_gun_by_hand_and_fires_the_loaded_round()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 20));
        var t72 = Unit(1, VehicleKind.T72, new(0, -20), true);
        s.Vehicles.AddRange([m1, t72]);
        m1.HoldFire = false;
        m1.TurretHeading = MathF.PI / 2;
        s.GunnerId = m1.Id;
        Run(s, 1);
        Assert.Equal(MathF.PI / 2, m1.TurretHeading);
        Assert.Equal(0, s.ShotsFired);

        s.GunnerTraverse = -1;
        Run(s, .5f);
        Assert.Equal(MathF.PI / 2 - m1.Spec.TurretTurn * .5f, m1.TurretHeading, .01f);

        m1.TurretHeading = 0;
        var range = s.RangeAlongGun(m1);
        Assert.NotNull(range);
        Assert.Same(t72, range.Value.Vehicle);
        Assert.Equal(40 - Vehicle.HitRadius, range.Value.Distance, .01f);

        Assert.True(s.FireGunner(m1));
        Assert.Equal(VehicleSpec.For(VehicleKind.Abrams).Magazine[(int)Weapon.Sabot] - 1, m1.Rounds[(int)Weapon.Sabot]);
        Assert.False(s.FireGunner(m1), "Fired again while reloading");

        Assert.Equal(Weapon.MachineGun, s.CycleAmmunition(m1));
        Assert.Equal(WeaponSpec.BurstSeconds, m1.Reload);
        Assert.Equal(Weapon.Heat, s.CycleAmmunition(m1));

        var itv = Unit(2, VehicleKind.Itv, new(30, 20));
        s.Vehicles.Add(itv);
        itv.Loaded = Weapon.Tow;
        itv.Speed = 4;
        Assert.False(s.FireGunner(itv), "Fired a missile on the move");
        itv.Speed = 0;
        Assert.True(s.FireGunner(itv));
    }

    [Fact]
    public void At_night_the_enemy_sees_less_far_unless_you_fire()
    {
        var campaign = Campaign.Start(GameMode.Classic);
        campaign.Island = Mission.ClassicNightIsland;
        var s = new Simulation(campaign);
        Assert.True(s.Night);
        s.Vehicles.Clear();
        s.Obstacles.Clear();
        s.Mines.Clear();
        s.Terrain!.Fill(Terrain.Open);
        s.Phase = MissionPhase.Active;
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 30));
        var t62 = Unit(20, VehicleKind.T62, new(0, -30), true);
        s.Vehicles.AddRange([m1, t62]);

        s.Recon.Refresh();
        Assert.True(s.Recon.Spotted(t62), "Thermal sights should see as far as by day");
        Assert.False(s.Recon.Spotted(m1), "The enemy saw 60 m in the dark");

        m1.Revealed = ClassicRules.RevealSeconds;
        s.Recon.Refresh();
        Assert.True(s.Recon.Spotted(m1), "A muzzle flash should show at night");

        Assert.False(new Simulation(Campaign.Start(GameMode.Classic)).Night);
        Assert.False(new Simulation(new Campaign { Island = Mission.ClassicNightIsland }).Night);
    }

    [Fact]
    public void Classic_garrisons_hold_fire_until_provoked_and_no_battle_opens_under_fire()
    {
        for (int island = 0; island < Mission.Count; island++)
        {
            for (int battle = 0; battle < ClassicCampaign.BattleCount(island); battle++)
            {
                var start = new Simulation(ClassicAt(island, battle));
                Assert.All(start.Vehicles.Where(v => !v.Convoy), v => Assert.True(v.Waiting, "Classic battles open with both sides waiting"));
                foreach (var defender in start.Vehicles.Where(v => v.Enemy))
                {
                    float nearest = start.Vehicles.Where(v => !v.Enemy).Min(v => Vector2.Distance(v.Position, defender.Position));
                    Assert.True(nearest > ClassicRules.EngageRange, $"Island {island} battle {battle} opens {nearest:0} m from a defender");
                }
            }
        }

        // A force that lands and waits is left alone: no fire, and no artillery on it either.
        var quiet = new Simulation(ClassicAt(0));
        quiet.Begin();
        Run(quiet, 60);
        Assert.All(quiet.Vehicles.Where(v => !v.Enemy), v => Assert.Equal(Vehicle.MaxHealth, v.Health));
        Assert.Empty(quiet.Strikes);

        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 30));
        var t62 = Unit(20, VehicleKind.T62, new(0, -30), true);
        var neighbour = Unit(21, VehicleKind.T62, new(20, -30), true);
        s.Vehicles.AddRange([m1, t62, neighbour]);
        t62.HoldFire = neighbour.HoldFire = false;
        t62.Waiting = neighbour.Waiting = true;
        Run(s, 5);
        Assert.Equal(0, s.ShotsFired);
        Assert.Equal(Vehicle.MaxHealth, m1.Health);

        t62.Health = 90;
        Run(s, .1f);
        Assert.False(t62.Waiting, "A defender that was hit kept holding its fire");
        Assert.False(neighbour.Waiting, "Its neighbour did not join in");

        var close = EmptyClassic();
        var scout = Unit(0, VehicleKind.M113, new(0, 10));
        var guard = Unit(20, VehicleKind.T62, new(0, -30), true);
        close.Vehicles.AddRange([scout, guard]);
        guard.HoldFire = false;
        guard.Waiting = true;
        Run(close, 1);
        Assert.False(guard.Waiting, "A defender let an enemy drive up to it");
    }

    [Fact]
    public void Every_classic_shell_reports_who_fired_it_and_what_it_did()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 20));
        var t62 = Unit(20, VehicleKind.T62, new(0, -20), true);
        s.Vehicles.AddRange([m1, t62]);
        Assert.True(s.FireGunner(m1));
        Run(s, 1);
        var hit = s.Events.Single(e => e.Kind is CombatEventKind.Impact or CombatEventKind.Destroy);
        Assert.Equal(m1.Id, hit.Shooter);
        Assert.Equal(t62.Id, hit.UnitId);
        Assert.Equal(Vehicle.MaxHealth - t62.Health, hit.Damage, .01f);

        s.Events.Clear();
        m1.Reload = 0;
        m1.TurretHeading = MathF.PI / 2;
        Assert.True(s.FireGunner(m1));
        Run(s, 2);
        var miss = s.Events.Single(e => e.Kind == CombatEventKind.Impact);
        Assert.Equal(m1.Id, miss.Shooter);
        Assert.Equal(-1, miss.UnitId);
    }

    private static int HitsOutOf(Simulation s, Vehicle shooter, Vehicle target, int shots, Terrain ground, bool aimed = true)
    {
        s.Terrain!.Paint(target.Position, target.Position, 3, ground);
        int hits = 0;
        for (int i = 0; i < shots; i++)
        {
            shooter.Loaded = Weapon.Sabot;
            shooter.Reload = 0;
            shooter.Rounds[(int)Weapon.Sabot] = 1;
            target.Health = Vehicle.MaxHealth;
            s.Recon.Refresh();
            Assert.True(aimed ? s.FireGunner(shooter) : s.TryFire(shooter, target));
            Run(s, 1);
            hits += target.Health < Vehicle.MaxHealth ? 1 : 0;
        }

        return hits;
    }
}
