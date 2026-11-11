namespace Breakwater;

/// <summary>
/// Capture sites: a ring that fills clockwise as control builds, the defenders' flag lowering as
/// ours rises up the same pole, a pulsing ring while the site is contested, and a burst when it is
/// secured.
/// </summary>
public partial class BattleView
{
    // Pole and flags, in metres.
    private const float PoleHeight = 6.4f;
    private const float FlagBottom = 1.2f, FlagTop = 5.6f;
    private static readonly Vector3 FlagSize = new(2.6f, 1.3f, .1f);

    /// <summary>Flags stream from the pole in the breeze: degrees of swing, and radians per second.</summary>
    private const float FlagSwingDegrees = 12, FlagSwingRate = 2.3f;

    // The progress band sits just inside the ring.
    private const float ProgressInset = .3f, ProgressWidth = 1.4f, ProgressHeight = .12f;
    private const int ProgressSegments = 72;

    /// <summary>A contested ring pulses this many times a second, swelling by this fraction.</summary>
    private const float ContestPulseRate = 2.2f, ContestSwell = .05f;

    private readonly List<SiteModel> sites = [];

    private void BuildSite(CaptureSite site)
    {
        var ring = Ring(this, World(site.Position, .1f), CaptureSite.FriendlyRadius, Palette.World.SiteRing);
        Box(this, World(site.Position, PoleHeight / 2), new(.18f, PoleHeight, .18f), Palette.World.FlagPole);

        var progress = new ImmediateMesh();
        AddChild(new MeshInstance3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
            },
            Mesh = progress,
            Position = World(site.Position, ProgressHeight),
        });

        // The defenders' flag streams east of the pole and ours west, so they pass rather than collide.
        sites.Add(new(ring, progress, Flag(site, Palette.World.Flag, 1), Flag(site, Palette.Groups[0], -1)));
    }

    /// <summary>A flag on a pivot at the pole, so it swings around the pole rather than its own centre.</summary>
    private Node3D Flag(CaptureSite site, Color color, int side)
    {
        var pivot = new Node3D { Position = World(site.Position, FlagTop) };
        AddChild(pivot);
        Box(pivot, new(side * FlagSize.X / 2, 0, 0), FlagSize, color);
        return pivot;
    }

    private void SyncSites(Simulation sim)
    {
        for (int i = 0; i < sites.Count; i++)
        {
            var site = sim.Sites[i];
            var model = sites[i];
            float f = site.Fraction;
            float swing = Mathf.DegToRad(FlagSwingDegrees) * Mathf.Sin(clock * FlagSwingRate + i);

            model.EnemyFlag.Visible = !site.Secured;
            model.EnemyFlag.Position = model.EnemyFlag.Position with { Y = Mathf.Lerp(FlagTop, FlagBottom, f) };
            model.EnemyFlag.Rotation = new(0, swing, 0);
            model.OurFlag.Visible = f > 0;
            model.OurFlag.Position = model.OurFlag.Position with { Y = Mathf.Lerp(FlagBottom, FlagTop, f) };
            model.OurFlag.Rotation = new(0, -swing, 0);

            float pulse = site.Contested ? (1 + Mathf.Sin(clock * ContestPulseRate * Mathf.Tau)) / 2 : 0;
            var ringColor = site.Secured ? Palette.Groups[0] : Palette.World.SiteRing.Lerp(Palette.Enemy, pulse);
            ((StandardMaterial3D)model.Ring.MaterialOverride).AlbedoColor = ringColor;
            model.Ring.Scale = Vector3.One * (1 + ContestSwell * pulse);

            var band = site.Contested ? Palette.World.ContestedBand : Palette.World.CaptureBand;
            if (f != model.Drawn || band != model.DrawnColor)
            {
                DrawProgress(model.Progress, f, band);
                sites[i] = model with { Drawn = f, DrawnColor = band };
            }
        }
    }

    /// <summary>An annulus just inside the ring, swept clockwise from north by <paramref name="fraction"/> of a turn.</summary>
    private static void DrawProgress(ImmediateMesh mesh, float fraction, Color color)
    {
        mesh.ClearSurfaces();
        int segments = (int)Mathf.Ceil(ProgressSegments * fraction);
        if (segments == 0)
        {
            return;
        }

        float outer = CaptureSite.FriendlyRadius - ProgressInset;
        float inner = outer - ProgressWidth;
        mesh.SurfaceBegin(Godot.Mesh.PrimitiveType.TriangleStrip);
        mesh.SurfaceSetColor(color);
        for (int s = 0; s <= segments; s++)
        {
            float angle = Mathf.Tau * fraction * s / segments;
            var direction = new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle));
            mesh.SurfaceAddVertex(direction * outer);
            mesh.SurfaceAddVertex(direction * inner);
        }

        mesh.SurfaceEnd();
    }

    /// <summary>A site was secured: a team-coloured shockwave and a fountain of sparks from the flagpole.</summary>
    private void CaptureBurst(Vector3 at)
    {
        var ramp = Ramp((0, Palette.World.FlashCore), (.3f, Palette.Groups[0]), (1, Palette.Groups[0] with { A = 0 }));
        var shock = Burst(at with { Y = ProgressHeight }, shockRing, additive, ramp);
        shock.Amount = 1;
        shock.Gravity = Vector3.Zero;
        shock.InitialVelocityMax = 0;
        shock.Lifetime = .9f;
        shock.ScaleAmountCurve = Line(.3f, 1);
        shock.ScaleAmountMin = shock.ScaleAmountMax = CaptureSite.FriendlyRadius * 1.6f;

        var fountain = Burst(at with { Y = FlagTop }, chip, additive, ramp);
        fountain.Amount = 60;
        fountain.Direction = Vector3.Up;
        fountain.Gravity = Vector3.Down * 9;
        fountain.InitialVelocityMin = 6;
        fountain.InitialVelocityMax = 14;
        fountain.Lifetime = 1.8f;
        fountain.ScaleAmountMin = 1.5f;
        fountain.ScaleAmountMax = 3;
        fountain.Spread = 35;
    }

    private sealed record SiteModel(MeshInstance3D Ring, ImmediateMesh Progress, Node3D EnemyFlag, Node3D OurFlag)
    {
        public float Drawn { get; init; } = -1;
        public Color DrawnColor { get; init; }
    }
}
