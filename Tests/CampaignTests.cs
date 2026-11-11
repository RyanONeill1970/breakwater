namespace Breakwater.Tests;

public class CampaignTests
{
    [Fact]
    public void Refit_exchange_preserves_health_and_cannot_manufacture_supplies()
    {
        var c = new Campaign();
        c.Roster[0].Health = 30;
        int supplies = c.Supplies;
        Assert.True(c.Refit(0));
        Assert.Equal(Loadout.Mixed, c.LoadoutOf(0));
        Assert.True(c.Refit(0));
        Assert.Equal(Loadout.Recon, c.LoadoutOf(0));
        Assert.True(c.Refit(0));
        Assert.Equal(Loadout.Armor, c.LoadoutOf(0));
        Assert.Equal(supplies, c.Supplies);
        Assert.Equal(30, c.Roster[0].Health);

        c.Refit(0);
        c.Refit(0);
        c.Supplies = 0;
        Assert.False(c.Refit(0), "Unaffordable refit accepted");
        Assert.Equal(Loadout.Recon, c.LoadoutOf(0));
        Assert.Equal(0, c.Supplies);
    }

    [Fact]
    public void Repairs_and_replacements_charge_only_the_requested_group()
    {
        var c = new Campaign();
        c.Roster[0].Health = 0;
        c.Roster[1].Health = 50;
        c.Roster[4].Health = 20;
        Assert.Equal(8, c.RepairCost(0));
        Assert.True(c.Repair(0));
        Assert.Equal(0, c.Roster[0].Health);
        Assert.Equal(100, c.Roster[1].Health);
        Assert.Equal(20, c.Roster[4].Health);

        Assert.True(c.Replace(0));
        Assert.Equal(52, c.Supplies);
        Assert.Equal(100, c.Roster[0].Health);

        c.Roster[0].Health = 0;
        c.Supplies = 10;
        Assert.False(c.Replace(0));
        Assert.Equal(0, c.Roster[0].Health);
    }

    [Fact]
    public void Victory_carries_damage_and_losses_and_retry_preserves_the_deployment_snapshot()
    {
        var s = new Simulation();
        s.Begin();
        s.Vehicles[0].Health = 0;
        s.Vehicles[1].Health = 45;
        s.Vehicles[1].Ammo = 2;
        s.Elapsed = 75;

        var retry = s.RetryIsland();
        Assert.Equal(0, retry.Campaign.Island);
        Assert.Equal(100, retry.Vehicles[0].Health);
        Assert.Equal(100, retry.Campaign.Supplies);
        Assert.Throws<InvalidOperationException>(() => s.NextIsland());

        s.Phase = MissionPhase.Victory;
        foreach (var site in s.Sites)
        {
            site.Progress = CaptureSite.HoldSeconds;
        }

        var next = s.NextIsland();
        Assert.Equal(1, next.Campaign.Island);
        Assert.Equal(200, next.Campaign.Supplies);
        Assert.Equal(75, next.Campaign.PreviousTime);
        Assert.Equal(1, next.Campaign.PreviousLosses);
        Assert.DoesNotContain(next.Vehicles, v => v.Id == 0);
        Assert.Equal(45, next.Vehicles.Single(v => v.Id == 1).Health);
        Assert.Equal(32, next.Vehicles.Single(v => v.Id == 1).Ammo);
        Assert.Equal(0, s.Campaign.Island);
        Assert.Equal(100, s.Campaign.Supplies);
    }

    [Fact]
    public void The_final_island_cannot_advance()
    {
        var final = new Simulation(new Campaign { Island = Mission.Count - 1 }) { Phase = MissionPhase.Victory };
        Assert.Throws<InvalidOperationException>(() => final.NextIsland());
    }

    [Fact]
    public void A_complete_three_island_campaign_can_be_won_with_persistent_survivors()
    {
        var s = new Simulation();
        for (int island = 0; island < Mission.Count; island++)
        {
            s.Begin();
            TestWorld.AssaultAllSites(s, 360);
            Assert.True(s.Phase == MissionPhase.Victory,
                $"Island {island + 1} did not complete: {s.Phase}, captures {string.Join(",", s.Sites.Select(o => o.Progress))}");
            if (island < Mission.Count - 1)
            {
                s = SaveGame.Read(SaveGame.Write(s)).NextIsland();
                var c = s.Campaign.Clone();
                for (int g = 0; g < Mission.GroupCount; g++)
                {
                    while (c.Replace(g))
                    {
                    }
                    c.Repair(g);
                }
                s = new Simulation(c);
            }
        }
    }
}
