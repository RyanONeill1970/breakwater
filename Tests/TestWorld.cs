namespace Breakwater.Tests;

using System.Numerics;

/// <summary>Builders for small sandbox battles.</summary>
internal static class TestWorld
{
    private const float StepSeconds = 1f / 60;

    /// <summary>
    /// An active mission with no vehicles, no cover, no reinforcements and one unreachable objective,
    /// so it never ends by itself.
    /// </summary>
    public static Simulation Empty()
    {
        var s = new Simulation { ReinforcementWaves = 0 };
        s.Vehicles.Clear();
        s.Obstacles.Clear();
        s.Sites.Clear();
        s.Sites.Add(new("Test", new(85, -85)));
        s.Phase = MissionPhase.Active;
        return s;
    }

    /// <summary>As <see cref="Empty"/>, but playing Classic rules on open ground that tests can repaint, with no enemy mines or guns.</summary>
    public static Simulation EmptyClassic()
    {
        var s = new Simulation(Campaign.Start(GameMode.Classic));
        s.Vehicles.Clear();
        s.Obstacles.Clear();
        s.Sites.Clear();
        s.Sites.Add(new("Test", new(85, -85)));
        s.Terrain!.Fill(Terrain.Open);
        s.Mines.Clear();
        s.EnemyArtilleryLeft = 0;
        s.Phase = MissionPhase.Active;
        return s;
    }

    /// <summary>A Classic campaign at <paramref name="island"/>'s <paramref name="battle"/>th battle (by default its last, the battle for its stations).</summary>
    public static Campaign ClassicAt(int island, int battle = -1)
    {
        var campaign = Campaign.Start(GameMode.Classic);
        campaign.Island = island;
        campaign.Battle = battle < 0 ? ClassicCampaign.FinalBattle(island) : battle;
        return campaign;
    }

    /// <summary>A fully loaded Classic vehicle with fire held and AI parked.</summary>
    public static Vehicle Unit(int id, VehicleKind kind, Vector2 position, bool enemy = false)
    {
        var v = new Vehicle
        {
            Id = id,
            Enemy = enemy,
            Group = enemy ? Mission.EnemyGroup : 0,
            HoldFire = true,
            Home = position,
            Kind = kind,
            Position = position,
            Think = 999,
        };
        v.Rearm();
        return v;
    }

    /// <summary>A main tank with fire held and AI parked, so tests control exactly what it does.</summary>
    public static Vehicle Tank(int id, Vector2 position, bool enemy = false) => new()
    {
        Id = id,
        Ammo = 32,
        Enemy = enemy,
        Group = enemy ? Mission.EnemyGroup : 0,
        HoldFire = true,
        Home = position,
        Position = position,
        Think = 999,
    };

    public static void Run(Simulation s, float seconds, int manualId = -1, float drive = 0)
    {
        for (int i = 0; i < (int)MathF.Round(seconds / StepSeconds); i++)
        {
            s.Step(StepSeconds, manualId, drive);
        }
    }

    /// <summary>
    /// Plays a mission by sending all four groups at the first objective not yet secured, turning back
    /// whenever a counter-attack takes one away.
    /// </summary>
    public static void AssaultAllSites(Simulation s, float maxSeconds, Action? everySecond = null)
    {
        int site = -1;
        for (int frame = 0; frame < MathF.Round(maxSeconds / StepSeconds) && s.Phase == MissionPhase.Active; frame++)
        {
            int target = s.Sites.FindIndex(o => !o.Secured);
            if (target != site)
            {
                site = target;
                OrderAll(s, s.Sites[site].Position, site == 0 ? 9 : 8);
            }

            s.Step(StepSeconds);
            s.Events.Clear();
            if (frame % 60 == 0)
            {
                everySecond?.Invoke();
            }
        }
    }

    public static void AssertNoOverlapsOrPenetration(Simulation s)
    {
        foreach (var a in s.Vehicles)
        {
            Assert.True(s.Navigation.Clear(a.Position, a.Position, Mission.VehicleRadius), $"Vehicle {a.Id} inside cover");
            Assert.DoesNotContain(s.Vehicles, b => a.Id < b.Id && Vector2.Distance(a.Position, b.Position) < Mission.VehicleRadius * 2 - .01f);
        }
    }

    private static void OrderAll(Simulation s, Vector2 site, float spacing)
    {
        for (int g = 0; g < Mission.GroupCount; g++)
        {
            s.Order(g, site + new Vector2((g - 1.5f) * spacing, 5));
        }
    }
}
