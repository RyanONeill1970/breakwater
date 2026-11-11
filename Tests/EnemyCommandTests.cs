namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class EnemyCommandTests
{
    /// <summary>A far-off friendly on the beach, so the island never falls to defeat.</summary>
    private static readonly Vector2 Beach = new(-80, 85);

    [Fact]
    public void Idle_defenders_relieve_a_site_under_capture_without_stripping_their_garrisons()
    {
        var s = Island();
        s.Sites[0].Progress = 3;
        Run(s, 1.1f);

        var relief = s.Vehicles.Where(v => v.Objective == 0).ToList();
        Assert.Equal(Simulation.ReliefSize, relief.Count);
        Assert.All(relief, v => Assert.Equal(Tactic.Assault, v.Tactic));
        Assert.All(relief, v => Assert.NotEmpty(v.Route));
        for (int site = 1; site < s.Sites.Count; site++)
        {
            Assert.True(Garrison(s, site) >= Simulation.GarrisonMinimum, $"Garrison {site} stripped");
        }

        Assert.Contains(s.Events, e => e.Kind == CombatEventKind.Assault && e.PlayerVisible && e.Message.Contains(s.Sites[0].Name));
    }

    [Fact]
    public void Only_one_force_is_sent_at_a_time()
    {
        var s = Island();
        s.Sites[0].Progress = 3;
        s.Sites[1].Progress = 3;
        Run(s, 1.1f);
        Assert.DoesNotContain(s.Vehicles, v => v.Objective == 1);

        // Still blocked once the cooldown passes, because the first force is alive.
        s.NextDispatch = 0;
        Run(s, 1.1f);
        Assert.DoesNotContain(s.Vehicles, v => v.Objective == 1);
    }

    [Fact]
    public void A_secured_site_left_undefended_is_counter_attacked_and_retaken()
    {
        var s = Island();
        var site = s.Sites[0];
        Capture(s, 0);
        Run(s, Simulation.CounterAttackDelay - 1);
        Assert.DoesNotContain(s.Vehicles, v => v.Objective != Vehicle.NoObjective);

        Run(s, 2);
        var attackers = s.Vehicles.Where(v => v.Objective == 0).ToList();
        Assert.Equal(Simulation.CounterAttackSize, attackers.Count);
        Assert.Contains(s.Events, e => e.Kind == CombatEventKind.Assault && e.Message.Contains("counter-attack"));

        Run(s, 90);
        Assert.False(site.Secured);
        Assert.Equal(0, site.Progress);
        Assert.Contains(s.Events, e => e.Kind == CombatEventKind.SiteLost && e.Message.Contains(site.Name));

        // The attackers dig in as the site's new garrison.
        Assert.All(attackers, v => Assert.Equal(Vehicle.NoObjective, v.Objective));
        Assert.All(attackers, v => Assert.True(Vector2.Distance(v.Home, site.Position) < CaptureSite.EnemyRadius));
    }

    [Fact]
    public void Reinforcements_land_in_the_north_and_counter_attack_a_held_site()
    {
        var s = Island(waves: 1);
        Capture(s, 1);
        s.Elapsed = Simulation.FirstWaveSeconds - .5f;
        int before = s.Vehicles.Count;
        Run(s, 1.1f);

        var landed = s.Vehicles.Skip(before).ToList();
        Assert.Equal(Mission.WaveSize, landed.Count);
        Assert.Equal(Enumerable.Range(Mission.FirstReinforcementId, Mission.WaveSize), landed.Select(v => v.Id));
        Assert.All(landed, v => Assert.True(v.Enemy && v.Alive && v.Objective == 1));
        Assert.All(landed, v => Assert.True(Vector2.Distance(v.Position, Mission.LandingPoint(0)) < 20));
        Assert.Contains(s.Events, e => e.Kind == CombatEventKind.Reinforcement && e.Message.Contains(s.Sites[1].Name));
        AssertNoOverlapsOrPenetration(s);

        Assert.Null(s.NextWave);
        Run(s, Simulation.WaveIntervalSeconds);
        Assert.Equal(before + Mission.WaveSize, s.Vehicles.Count);
    }

    [Fact]
    public void Without_a_held_site_reinforcements_join_the_weakest_garrison()
    {
        var s = Island(waves: 1);
        foreach (var defender in s.Vehicles.Where(v => v.Enemy && Vector2.Distance(v.Home, s.Sites[2].Position) < CaptureSite.EnemyRadius).Take(3))
        {
            defender.Health = 0;
        }

        s.Elapsed = Simulation.FirstWaveSeconds - .5f;
        Run(s, 1.1f);

        var landed = s.Vehicles.Where(v => v.Id >= Mission.FirstReinforcementId).ToList();
        Assert.Equal(Mission.WaveSize, landed.Count);
        Assert.All(landed, v => Assert.Equal(Vehicle.NoObjective, v.Objective));
        Assert.All(landed, v => Assert.True(Vector2.Distance(v.Home, s.Sites[2].Position) < CaptureSite.EnemyRadius * 2));
        Assert.Contains(s.Events, e => e.Kind == CombatEventKind.Reinforcement);
    }

    [Fact]
    public void Landed_reinforcements_and_assault_orders_survive_a_quicksave()
    {
        var s = Island(waves: 2);
        Capture(s, 1);
        s.Elapsed = Simulation.FirstWaveSeconds - .5f;
        Run(s, 1.1f);
        s.Shells.Clear();

        var loaded = SaveGame.Read(SaveGame.Write(s));

        Assert.Equal(s.Vehicles.Count, loaded.Vehicles.Count);
        Assert.Equal(1, loaded.WavesLanded);
        Assert.Equal(s.NextDispatch, loaded.NextDispatch);
        Assert.Equal(s.Sites[1].SecuredFor, loaded.Sites[1].SecuredFor);
        foreach (var v in s.Vehicles)
        {
            var copy = loaded.Vehicles.Single(l => l.Id == v.Id);
            Assert.Equal(v.Home, copy.Home);
            Assert.Equal(v.Objective, copy.Objective);
            Assert.Equal(v.Position, copy.Position);
        }

        var json = JsonNode.Parse(SaveGame.Write(s))!;
        json["WavesLanded"] = 0;
        Assert.Throws<FormatException>(() => SaveGame.Read(json.ToJsonString()));

        json = JsonNode.Parse(SaveGame.Write(s))!;
        json["Vehicles"]![s.Vehicles.Count - 1]!["Objective"] = s.Sites.Count;
        Assert.Throws<FormatException>(() => SaveGame.Read(json.ToJsonString()));
    }

    /// <summary>Island one with its real garrisons and a single live friendly parked on the beach.</summary>
    private static Simulation Island(int waves = 0)
    {
        var s = new Simulation { ReinforcementWaves = waves };
        foreach (var v in s.Vehicles.Where(v => !v.Enemy && v.Id != 0))
        {
            v.Health = 0;
        }

        s.Vehicles[0].Position = Beach;
        s.Begin();
        return s;
    }

    /// <summary>Hands a site to the player as if they had just taken it: secured, its garrison destroyed.</summary>
    private static void Capture(Simulation s, int site)
    {
        s.Sites[site].Progress = CaptureSite.HoldSeconds;
        foreach (var v in s.Vehicles.Where(v => v.Enemy && Vector2.Distance(v.Home, s.Sites[site].Position) < CaptureSite.EnemyRadius))
        {
            v.Health = 0;
        }
    }

    private static int Garrison(Simulation s, int site) => s.Vehicles.Count(v =>
        v.Alive && v.Enemy && v.Objective == Vehicle.NoObjective && Vector2.Distance(v.Home, s.Sites[site].Position) < CaptureSite.EnemyRadius);
}
