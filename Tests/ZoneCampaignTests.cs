namespace Breakwater.Tests;

using static TestWorld;

public class ZoneCampaignTests
{
    [Fact]
    public void Every_classic_battle_is_well_formed()
    {
        for (int island = 0; island < Mission.Count; island++)
        {
            for (int battle = 0; battle < ClassicCampaign.BattleCount(island); battle++)
            {
                var spec = ClassicCampaign.Battle(island, battle);
                var s = new Simulation(ClassicAt(island, battle));
                string where = $"island {island} battle {battle}";
                Assert.True(spec.Zone is >= 0 and < ZoneBattle.ZoneCount, where);
                Assert.True(spec.Garrison.Count <= Mission.DefenderCount, where);
                AssertNoOverlapsOrPenetration(s);
                Assert.All(s.Sites, site => Assert.True(s.Navigation.Clear(site.Position, site.Position, Mission.VehicleRadius), where));
                switch (spec.Objective)
                {
                    case ObjectiveKind.Capture:
                        Assert.NotEmpty(s.Sites);
                        break;
                    case ObjectiveKind.Destroy:
                        Assert.Equal(ObstacleKind.Building, s.TargetBuilding!.Kind);
                        Assert.False(s.Civilian(s.TargetBuilding), "The target counted as civilian");
                        break;
                    case ObjectiveKind.Clear:
                        Assert.NotEmpty(spec.Garrison);
                        break;
                }

                Assert.False(s.ObjectiveMet(), $"{where} is won before it starts");
            }

            Assert.Equal(ObjectiveKind.Capture, ClassicCampaign.Battle(island, ClassicCampaign.FinalBattle(island)).Objective);
        }
    }

    [Fact]
    public void Victory_advances_battle_by_battle_then_island_by_island()
    {
        var campaign = Campaign.Start(GameMode.Classic);
        List<(int, int)> order = [];
        while (true)
        {
            order.Add((campaign.Island, campaign.Battle));
            var s = new Simulation(campaign) { Phase = MissionPhase.Victory };
            if (Mission.FinalBattle(campaign))
            {
                Assert.Throws<InvalidOperationException>(() => s.Campaign.AfterVictory(s));
                break;
            }

            campaign = s.Campaign.AfterVictory(s);
            Assert.True(campaign.Valid());
        }

        var expected = Enumerable.Range(0, Mission.Count)
            .SelectMany(i => Enumerable.Range(0, ClassicCampaign.BattleCount(i)).Select(b => (i, b)))
            .ToList();
        Assert.Equal(expected, order);
        Assert.True(order.Count > Mission.Count, "Some island should have more than one battle");
    }

    [Fact]
    public void Destroy_and_clear_battles_are_won_by_their_objectives()
    {
        var radar = new Simulation(ClassicAt(1, 0));
        radar.Begin();
        int target = radar.Layout.ToList().IndexOf(radar.TargetBuilding!);
        radar.BuildingHealth[target] = 0;
        radar.ApplyBuildingDamage();
        radar.Step(.1f);
        Assert.Equal(MissionPhase.Victory, radar.Phase);

        var jungle = new Simulation(ClassicAt(2, 0));
        jungle.Begin();
        foreach (var enemy in jungle.Vehicles.Where(v => v.Enemy))
        {
            enemy.Health = 0;
        }

        jungle.Step(.1f);
        Assert.Equal(MissionPhase.Victory, jungle.Phase);
    }

    [Fact]
    public void The_first_training_battle_is_won_by_driving_to_the_relay()
    {
        var s = new Simulation(Campaign.Start(GameMode.Classic));
        s.Begin();
        Assert.DoesNotContain(s.Vehicles, v => v.Enemy);
        AssaultAllSites(s, 120);
        Assert.Equal(MissionPhase.Victory, s.Phase);
    }

    [Fact]
    public void Classic_saves_from_before_the_zone_campaign_resume_at_the_island_battle()
    {
        var s = new Simulation(ClassicAt(1));
        s.Begin();
        var json = JsonNode.Parse(SaveGame.Write(s))!;
        json["Version"] = 7;
        json["Campaign"]!.AsObject().Remove("Battle");
        var loaded = SaveGame.Read(json.ToJsonString());
        Assert.Equal(ClassicCampaign.FinalBattle(1), loaded.Campaign.Battle);
        Assert.Equal(s.Vehicles.Count, loaded.Vehicles.Count);
    }
}
