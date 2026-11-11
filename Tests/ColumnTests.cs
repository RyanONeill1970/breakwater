namespace Breakwater.Tests;

using static TestWorld;

public class ColumnTests
{
    private const int AmbushBattle = 2;
    private const int HoldBattle = 1;

    [Fact]
    public void An_ambushed_convoy_arrives_on_time_and_the_battle_is_won_when_enough_are_destroyed()
    {
        var s = new Simulation(ClassicAt(0, AmbushBattle));
        s.Begin();
        var columns = s.ZoneBattle!.Columns!;
        s.Elapsed = columns[0].At;
        s.Step(.1f);
        var convoy = s.Vehicles.Where(v => v.Id >= Mission.FirstReinforcementId).ToList();
        Assert.Equal(columns[0].Kinds.Count, convoy.Count);
        Assert.All(convoy, v => Assert.NotEmpty(v.Orders));

        foreach (var v in convoy.Take(3))
        {
            v.Health = 0;
        }

        foreach (var friendly in s.Vehicles.Where(v => !v.Enemy))
        {
            friendly.HoldFire = true;
        }

        Run(s, 240);
        Assert.Equal(MissionPhase.Victory, s.Phase);
        Assert.Equal(2, s.Escaped);
    }

    [Fact]
    public void Letting_the_convoy_get_away_fails_the_ambush()
    {
        var s = new Simulation(ClassicAt(0, AmbushBattle));
        s.Begin();
        foreach (var friendly in s.Vehicles.Where(v => !v.Enemy))
        {
            friendly.HoldFire = true;
        }

        Run(s, 300);
        Assert.Equal(MissionPhase.Defeat, s.Phase);
        Assert.NotNull(s.BrokenCondition());
    }

    [Fact]
    public void A_hold_battle_is_won_only_by_holding_the_site_when_time_is_up()
    {
        var s = new Simulation(ClassicAt(1, HoldBattle));
        s.Begin();
        var site = s.Sites.Single();
        site.Progress = CaptureSite.HoldSeconds;
        s.Elapsed = ClassicCampaign.HoldSeconds - 1;
        Assert.False(s.ObjectiveMet());
        s.Elapsed = ClassicCampaign.HoldSeconds + 1;
        Assert.True(s.ObjectiveMet());
        site.Progress = 0;
        Assert.False(s.ObjectiveMet());

        var fresh = new Simulation(ClassicAt(1, HoldBattle));
        fresh.Begin();
        fresh.Elapsed = fresh.ZoneBattle!.Columns![1].At;
        fresh.Step(.1f);
        Assert.Equal(2, fresh.WavesLanded);
        Assert.True(fresh.Threatened(0), "The columns should be seen heading for the site");
    }

    [Fact]
    public void Saves_keep_column_vehicles_ids_and_escapes()
    {
        var s = new Simulation(ClassicAt(0, AmbushBattle));
        s.Begin();
        s.Elapsed = s.ZoneBattle!.Columns![0].At;
        s.Step(.1f);
        var first = s.Vehicles.First(v => v.Id >= Mission.FirstReinforcementId);
        s.Recon.FriendlyIntel[first.Id] = new Contact
        {
            Id = first.Id,
            Position = first.Position,
            LastSeen = s.Elapsed,
        };
        s.Vehicles.Remove(first);
        s.Escaped = 1;

        var loaded = SaveGame.Read(SaveGame.Write(s));
        Assert.Equal(1, loaded.Escaped);
        Assert.False(loaded.Recon.FriendlyIntel.ContainsKey(first.Id));
        Assert.Equal(s.Vehicles.Select(v => (v.Id, v.Kind)), loaded.Vehicles.Select(v => (v.Id, v.Kind)));
    }
}
