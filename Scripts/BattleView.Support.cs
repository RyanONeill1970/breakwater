namespace Breakwater;

/// <summary>
/// Classic support weapons in the 3D view: smoke screens as banks of grey puffs that thin out as
/// they clear, the player's own mines (the enemy's stay hidden until one goes off), and a ring
/// where each announced fire mission will land.
/// </summary>
public partial class BattleView
{
    // A smoke screen is a bank of puffs spread over its radius, this tall, thinning over its last seconds.
    private const int SmokePuffs = 7;
    private const float SmokeHeight = 2.2f, SmokeFadeSeconds = 8, SmokeAlpha = .8f;
    private const int SmokeSeed = 19;

    private const float MineRadius = .45f, MineHeight = .06f, MarkerHeight = .12f;

    /// <summary>A fire mission's ring shows about where its rounds will fall, in world metres.</summary>
    private static readonly float StrikeRingRadius = ClassicRules.Distance(320);

    private readonly Dictionary<SmokeCloud, (Node3D Node, StandardMaterial3D Material)> smokeScreens = [];
    private readonly Dictionary<Mine, Node3D> mineMarkers = [];
    private readonly Dictionary<ArtilleryStrike, Node3D> strikeMarkers = [];
    private readonly Random smokeRandom = new(SmokeSeed);

    private void SyncSupport(Simulation sim)
    {
        foreach (var cloud in sim.Smoke.Where(c => !smokeScreens.ContainsKey(c)))
        {
            smokeScreens[cloud] = BuildSmoke(cloud);
        }

        foreach (var (cloud, screen) in smokeScreens)
        {
            float left = cloud.Until - sim.Elapsed;
            screen.Material.AlbedoColor = Palette.World.SmokeScreen with { A = SmokeAlpha * Math.Clamp(left / SmokeFadeSeconds, 0, 1) };
        }

        Prune(smokeScreens, c => sim.Smoke.Contains(c), s => s.Node);
        foreach (var mine in sim.Mines.Where(m => !m.Enemy && !mineMarkers.ContainsKey(m)))
        {
            var marker = new Node3D { Position = World(mine.Position) };
            AddChild(marker);
            Mesh(marker, new(0, MineHeight / 2, 0), new CylinderMesh { BottomRadius = MineRadius, Height = MineHeight, TopRadius = MineRadius }, Palette.World.Mine);
            Ring(marker, new(0, MarkerHeight, 0), Mission.VehicleRadius, Palette.World.Mine);
            mineMarkers[mine] = marker;
        }

        Prune(mineMarkers, m => sim.Mines.Contains(m), n => n);
        foreach (var strike in sim.Strikes.Where(s => !strikeMarkers.ContainsKey(s)))
        {
            strikeMarkers[strike] = Ring(this, World(strike.Target, MarkerHeight), StrikeRingRadius, strike.Enemy ? Palette.Ui.Alert : Palette.World.FireMission);
        }

        Prune(strikeMarkers, s => sim.Strikes.Contains(s), n => n);
    }

    private (Node3D Node, StandardMaterial3D Material) BuildSmoke(SmokeCloud cloud)
    {
        var node = new Node3D { Position = World(cloud.Position) };
        AddChild(node);
        var material = new StandardMaterial3D
        {
            AlbedoColor = Palette.World.SmokeScreen,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var mesh = new SphereMesh { Height = SmokeHeight * 2, Radius = cloud.Radius / 2 };
        for (int i = 0; i < SmokePuffs; i++)
        {
            float angle = i * Mathf.Tau / SmokePuffs + smokeRandom.NextSingle();
            float reach = i == 0 ? 0 : cloud.Radius * (.35f + smokeRandom.NextSingle() * .3f);
            node.AddChild(new MeshInstance3D
            {
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = material,
                Mesh = mesh,
                Position = new(Mathf.Cos(angle) * reach, SmokeHeight * (.7f + smokeRandom.NextSingle() * .5f), Mathf.Sin(angle) * reach),
            });
        }

        return (node, material);
    }

    /// <summary>Frees the nodes of entries the simulation no longer has.</summary>
    private static void Prune<TKey, TValue>(Dictionary<TKey, TValue> shown, Func<TKey, bool> alive, Func<TValue, Node> node)
        where TKey : notnull
    {
        foreach (var key in shown.Keys.Where(k => !alive(k)).ToList())
        {
            node(shown[key]).QueueFree();
            shown.Remove(key);
        }
    }
}
