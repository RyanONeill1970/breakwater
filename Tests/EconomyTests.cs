namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class EconomyTests
{
    [Fact]
    public void Classic_campaigns_count_dollars_at_reference_prices()
    {
        var c = Campaign.Start(GameMode.Classic);
        Assert.Equal(Campaign.ClassicStartingFunds, c.Supplies);
        Assert.Equal("$55M", Campaign.Money(c.Supplies, GameMode.Classic));
        Assert.Equal("$2.5M", Campaign.Money(Campaign.Price(VehicleKind.Abrams), GameMode.Classic));
        Assert.Equal("$200k", Campaign.Money(Campaign.Price(VehicleKind.Bmp), GameMode.Classic));
        Assert.Equal("$0", Campaign.Money(0, GameMode.Classic));
        Assert.Equal("100 supplies", Campaign.Money(100, GameMode.Modern));

        // A quarter of an M1's health costs $250k, as in the reference.
        c.Roster[0].Health = 75;
        Assert.Equal(25, c.RepairCost(0));
    }

    [Fact]
    public void Gunfire_knocks_down_a_civilian_building_opens_the_way_and_costs_a_fine()
    {
        var s = new Simulation(Campaign.Start(GameMode.Classic));
        s.Begin();
        var building = s.Obstacles.First(s.Civilian);
        var m1 = Unit(0, VehicleKind.Abrams, building.Center + new Vector2(0, building.HalfSize.Y + 12));
        s.Vehicles.RemoveAll(v => !v.Enemy);
        s.Vehicles.Add(m1);
        s.GunnerId = m1.Id;

        for (int shot = 0; shot < 60 && s.Obstacles.Contains(building); shot++)
        {
            m1.Reload = 0;
            m1.Rounds[(int)Weapon.Sabot] = 1;
            s.FireGunner(m1);
            Run(s, .5f);
        }

        Assert.DoesNotContain(building, s.Obstacles);
        Assert.Equal(0, s.BuildingHealth[s.Layout.ToList().IndexOf(building)]);
        Assert.Equal(Campaign.CivilianFine, s.Fines);
        Assert.True(s.Navigation.Clear(building.Center, building.Center), "The rubble still blocks the way");
    }

    [Fact]
    public void Destroying_enemy_vehicles_pays_a_bounty_and_victory_settles_the_account()
    {
        var s = EmptyClassic();
        var m1 = Unit(0, VehicleKind.Abrams, new(0, 20));
        s.Vehicles.Add(m1);
        s.LayMine(0);
        s.Vehicles.Add(Unit(20, VehicleKind.Btr, s.Mines.Single().Position, true));
        Run(s, Simulation.MineArmSeconds + 1);
        Assert.Equal(Campaign.BtrPrice, s.Bounty);

        var won = new Simulation(Campaign.Start(GameMode.Classic)) { Bounty = 300, Fines = 200, Phase = MissionPhase.Victory };
        var next = won.Campaign.AfterVictory(won);
        Assert.Equal(Campaign.ClassicStartingFunds + Campaign.ClassicVictoryPay + 100, next.Supplies);
    }

    [Fact]
    public void Saves_keep_rubble_bounties_and_fines()
    {
        var s = new Simulation(Campaign.Start(GameMode.Classic));
        s.Begin();
        int index = s.Layout.ToList().FindIndex(o => o.Kind == ObstacleKind.Building);
        s.BuildingHealth[index] = 0;
        s.ApplyBuildingDamage();
        s.Bounty = 80;
        s.Fines = 200;

        var loaded = SaveGame.Read(SaveGame.Write(s));
        Assert.Equal(s.BuildingHealth, loaded.BuildingHealth);
        Assert.DoesNotContain(s.Layout[index], loaded.Obstacles);
        Assert.Equal(80, loaded.Bounty);
        Assert.Equal(200, loaded.Fines);

        var json = JsonNode.Parse(SaveGame.Write(s))!;
        int ridge = s.Layout.ToList().FindIndex(o => o.Kind == ObstacleKind.Ridge);
        json["BuildingHealth"]![ridge] = 10;
        Assert.Throws<FormatException>(() => SaveGame.Read(json.ToJsonString()));
    }
}
