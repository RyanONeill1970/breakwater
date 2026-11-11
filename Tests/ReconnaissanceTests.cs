namespace Breakwater.Tests;

using System.Numerics;
using static TestWorld;

public class ReconnaissanceTests
{
    [Fact]
    public void Recon_uses_cover_and_scout_range_for_both_teams()
    {
        var s = Empty();
        var a = Tank(0, Vector2.Zero);
        var b = Tank(1, new(70, 0), true);
        s.Vehicles.AddRange([a, b]);
        Assert.False(s.Recon.CanObserve(a, b.Position), "Sight exceeds tank range");
        Assert.False(s.Recon.CanObserve(b, a.Position), "Sight exceeds tank range");

        a.Kind = VehicleKind.Scout;
        Assert.True(s.Recon.CanObserve(a, b.Position), "Scouts have no range advantage");

        s.Obstacles.Add(new(new(35, 0), new(2, 8), 5, ObstacleKind.Ridge));
        Assert.False(s.Recon.CanObserve(a, b.Position), "Scouting sees through cover");
        Assert.False(s.Recon.CanObserve(b, a.Position), "Scouting sees through cover");

        a.Health = 0;
        s.Obstacles.Clear();
        Assert.False(s.Recon.CanObserve(a, b.Position), "Destroyed observer grants vision");
    }

    [Fact]
    public void Hidden_contacts_retain_their_last_position_and_expire()
    {
        var s = Empty();
        var a = Tank(0, Vector2.Zero);
        var b = Tank(1, new(25, 0), true);
        s.Vehicles.AddRange([a, b]);
        s.Recon.Refresh();
        Assert.True(s.Recon.IsVisible(b), "Visible contact not revealed");

        b.Position = new(80, 0);
        s.Elapsed = 4;
        s.Recon.Refresh();
        Assert.False(s.Recon.IsVisible(b));
        Assert.Equal(new Vector2(25, 0), s.Recon.Ghosts.Single().Position);
        Assert.Contains(true, s.Recon.Explored);

        s.Elapsed = Reconnaissance.MemorySeconds + 1;
        s.Recon.Refresh();
        Assert.Empty(s.Recon.Ghosts);
    }
}
