namespace Breakwater.Tests;

using static TestWorld;

public class ConditionTests
{
    [Fact]
    public void Each_classic_island_sets_one_condition_and_modern_sets_none()
    {
        Assert.Equal(Mission.ClassicLossLimit, new Simulation(ClassicAt(0)).Conditions.MaxLosses);
        Assert.Equal(Mission.ClassicTimeLimitSeconds, new Simulation(ClassicAt(1, 0)).Conditions.TimeLimitSeconds);
        Assert.True(new Simulation(ClassicAt(2, 0)).Conditions.SpareCivilians);
        Assert.Equal(new BattleConditions(), new Simulation().Conditions);
    }

    [Fact]
    public void Losing_too_many_vehicles_fails_the_mission_and_the_failure_saves()
    {
        var s = new Simulation(ClassicAt(0));
        s.Begin();
        foreach (var v in s.Vehicles.Where(v => !v.Enemy).Take(Mission.ClassicLossLimit))
        {
            v.Health = 0;
        }

        s.Step(.1f);
        Assert.Equal(MissionPhase.Active, s.Phase);

        s.Vehicles.First(v => !v.Enemy && v.Alive).Health = 0;
        s.Step(.1f);
        Assert.Equal(MissionPhase.Defeat, s.Phase);
        Assert.NotNull(s.BrokenCondition());
        Assert.Equal(MissionPhase.Defeat, SaveGame.Read(SaveGame.Write(s)).Phase);
    }

    [Fact]
    public void Running_out_of_time_or_hitting_the_village_fails_the_mission()
    {
        var s = new Simulation(ClassicAt(1, 0));
        s.Begin();
        s.Elapsed = Mission.ClassicTimeLimitSeconds + 1;
        s.Step(.1f);
        Assert.Equal(MissionPhase.Defeat, s.Phase);

        s = new Simulation(ClassicAt(2, 0));
        s.Begin();
        s.Fines = Campaign.CivilianFine;
        s.Step(.1f);
        Assert.Equal(MissionPhase.Defeat, s.Phase);
    }
}
