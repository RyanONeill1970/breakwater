namespace Breakwater.Tests;

using System.Numerics;

public class SaveGameTests
{
    [Fact]
    public void Quicksave_roundtrip_preserves_mission_state()
    {
        var s = new Simulation();
        s.Begin();
        s.Elapsed = 55;
        s.Vehicles[0].Health = 62;
        s.Sites[0].Progress = 4;
        s.Order(0, new(-50, 40));

        var loaded = SaveGame.Read(SaveGame.Write(s));

        Assert.Equal(55, loaded.Elapsed);
        Assert.Equal(62, loaded.Vehicles[0].Health);
        Assert.Equal(4, loaded.Sites[0].Progress);
        Assert.True(loaded.Paused);
        Assert.NotEmpty(loaded.Vehicles[0].Route);
    }

    [Fact]
    public void Unsupported_versions_are_rejected()
    {
        var saved = SaveGame.Write(new Simulation());
        Assert.Throws<FormatException>(() => SaveGame.Read(saved.Replace($"\"Version\": {SaveGame.CurrentVersion}", "\"Version\": 99")));
    }

    [Fact]
    public void Saves_contain_only_persistent_vehicle_state()
    {
        var s = new Simulation();
        s.Begin();
        var vehicle = JsonNode.Parse(SaveGame.Write(s))!["Vehicles"]![0]!.AsObject();
        Assert.False(vehicle.ContainsKey("Route"));
        Assert.False(vehicle.ContainsKey("Status"));
        Assert.False(vehicle.ContainsKey("Speed"));
        Assert.False(vehicle.ContainsKey("Home"));
    }

    [Fact]
    public void Older_saves_with_runtime_vehicle_fields_still_load()
    {
        var s = new Simulation();
        s.Begin();
        var json = JsonNode.Parse(SaveGame.Write(s))!;
        json["Version"] = 3;
        var vehicle = json["Vehicles"]![0]!.AsObject();
        vehicle["Status"] = "Moving";
        vehicle["Speed"] = 4.5f;
        vehicle["Stuck"] = .2f;
        vehicle["Think"] = 1;
        vehicle["Route"] = new JsonArray(new JsonObject { ["X"] = 1, ["Y"] = 2 });
        vehicle["Home"] = new JsonObject { ["X"] = 1, ["Y"] = 2 };

        var loaded = SaveGame.Read(json.ToJsonString());

        Assert.Equal(0, loaded.Vehicles[0].Speed);
        Assert.Equal(s.Vehicles[0].Home, loaded.Vehicles[0].Home);
    }

    [Fact]
    public void Campaign_saves_restore_briefing_sparse_rosters_victory_and_legacy_checkpoints()
    {
        var c = new Campaign { Island = 1, Supplies = 37 };
        c.Roster[0].Health = 0;
        c.Roster[1].Health = 45;
        var s = new Simulation(c);
        var loaded = SaveGame.Read(SaveGame.Write(s));
        Assert.Equal(1, loaded.Campaign.Island);
        Assert.Equal(37, loaded.Campaign.Supplies);
        Assert.Equal(MissionPhase.Briefing, loaded.Phase);
        Assert.DoesNotContain(loaded.Vehicles, v => v.Id == 0);

        s.Begin();
        s.Vehicles.Single(v => v.Id == 1).Health = 30;
        loaded = SaveGame.Read(SaveGame.Write(s));
        Assert.Equal(MissionPhase.Active, loaded.Phase);
        Assert.True(loaded.Paused);
        Assert.Equal(30, loaded.Vehicles.Single(v => v.Id == 1).Health);

        s.Phase = MissionPhase.Victory;
        foreach (var site in s.Sites)
        {
            site.Progress = CaptureSite.HoldSeconds;
        }

        loaded = SaveGame.Read(SaveGame.Write(s));
        Assert.Equal(2, loaded.NextIsland().Campaign.Island);

        var old = new Simulation();
        old.Begin();
        var json = JsonNode.Parse(SaveGame.Write(old))!;
        json["Version"] = 1;
        json.AsObject().Remove("Campaign");
        json.AsObject().Remove("Phase");
        loaded = SaveGame.Read(json.ToJsonString());
        Assert.Equal(0, loaded.Campaign.Island);
        Assert.Equal(MissionPhase.Active, loaded.Phase);
    }

    [Fact]
    public void Classic_saves_keep_the_mode_and_each_vehicles_rounds()
    {
        var s = new Simulation(Campaign.Start(GameMode.Classic));
        s.Begin();
        var m1 = s.Vehicles[0];
        m1.Rounds[(int)Weapon.Sabot] = 3;
        m1.Loaded = Weapon.Sabot;
        m1.Reload = 2;
        m1.Revealed = 7;

        var loaded = SaveGame.Read(SaveGame.Write(s));
        var restored = loaded.Vehicles[0];
        Assert.Equal(GameMode.Classic, loaded.Campaign.Mode);
        Assert.NotNull(loaded.Terrain);
        Assert.Equal(m1.Rounds, restored.Rounds);
        Assert.Equal(Weapon.Sabot, restored.Loaded);
        Assert.Equal(2, restored.Reload);
        Assert.Equal(7, restored.Revealed);

        var json = JsonNode.Parse(SaveGame.Write(s))!;
        json["Vehicles"]![0]!["Rounds"]![(int)Weapon.Sabot] = 999;
        Assert.Throws<FormatException>(() => SaveGame.Read(json.ToJsonString()));

        // Saves from before Classic mode carry no mode and play Modern.
        json = JsonNode.Parse(SaveGame.Write(new Simulation()))!;
        json["Version"] = 4;
        json["Campaign"]!.AsObject().Remove("Mode");
        Assert.Equal(GameMode.Modern, SaveGame.Read(json.ToJsonString()).Campaign.Mode);
    }

    [Fact]
    public void Classic_saves_keep_smoke_mines_and_fire_missions()
    {
        var s = new Simulation(Campaign.Start(GameMode.Classic));
        s.Begin();
        s.FireSmoke(0);
        s.LayMine(1);
        s.CallArtillery(new(0, -40), ArtilleryShell.Cluster);

        var loaded = SaveGame.Read(SaveGame.Write(s));
        Assert.Equal(s.Smoke, loaded.Smoke);
        Assert.Equal(s.Mines, loaded.Mines);
        Assert.Equal(s.MinesLeft, loaded.MinesLeft);
        Assert.Equal(s.ArtilleryLeft, loaded.ArtilleryLeft);
        Assert.Equal(ArtilleryShell.Cluster, loaded.Strikes.Single().Shell);
        Assert.Equal(s.Vehicles[0].Smoke, loaded.Vehicles[0].Smoke);

        var json = JsonNode.Parse(SaveGame.Write(s))!;
        json["Strikes"]![0]!["RoundsLeft"] = 99;
        Assert.Throws<FormatException>(() => SaveGame.Read(json.ToJsonString()));
    }

    [Fact]
    public void Invalid_campaign_saves_and_impossible_outcomes_are_rejected()
    {
        var s = new Simulation();
        s.Begin();
        var json = JsonNode.Parse(SaveGame.Write(s))!;
        json["Campaign"]!["Supplies"] = -10;
        Assert.Throws<FormatException>(() => SaveGame.Read(json.ToJsonString()));

        json["Campaign"]!["Supplies"] = 100;
        json["Phase"] = (int)MissionPhase.Victory;
        Assert.Throws<FormatException>(() => SaveGame.Read(json.ToJsonString()));
    }

    [Fact]
    public void Fog_exploration_and_stale_contacts_survive_campaign_checkpoints()
    {
        var s = new Simulation();
        s.Begin();
        var target = s.Vehicles.First(v => v.Enemy);
        target.Position = new(0, 45);
        s.Recon.Refresh();
        target.Position = new(75, -80);
        s.Elapsed = 5;
        s.Recon.Refresh();
        Assert.Contains(s.Recon.Ghosts, c => c.Id == target.Id);

        var loaded = SaveGame.Read(SaveGame.Write(s));
        Assert.Contains(loaded.Recon.Ghosts, c => c.Id == target.Id && c.Position == new Vector2(0, 45));
        Assert.Equal(s.Recon.Explored, loaded.Recon.Explored);

        var json = JsonNode.Parse(SaveGame.Write(s))!;
        json["Version"] = 2;
        json.AsObject().Remove("Recon");
        Assert.Equal(0, SaveGame.Read(json.ToJsonString()).Campaign.Island);
    }
}
