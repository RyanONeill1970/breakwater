namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>Screen-space labels drawn over the 3D battlefield: health bars, hit rings, armour-hit callouts, site progress and last-known contacts.</summary>
public partial class WorldOverlay : Control
{
    /// <summary>How long a hit ring stays on a damaged vehicle, in simulation seconds.</summary>
    private const float HitRingSeconds = 1.4f;

    // Anchor heights above the ground, in metres.
    private const float VehicleLabelHeight = 4;
    private const float SiteLabelHeight = 8;
    private const float GhostLabelHeight = 3;

    // Sizes in pixels.
    private const float HealthBarWidth = 26, HealthBarHeight = 3;
    private const float HitRingRadius = 19, GhostRadius = 7;
    private const int SiteFontSize = 14, GhostFontSize = 12;
    private static readonly Vector2 SiteLabelOffset = new(-35, -4);
    private static readonly Vector2 GhostLabelOffset = new(10, 4);

    // Hit callouts ("RICOCHET", "FLANK HIT") float up from the impact and fade.
    private const float CalloutSeconds = 1.3f;
    private const float CalloutHeight = 3;
    private const float CalloutRise = 38;
    private const int CalloutFontSize = 15, KillFontSize = 19, CalloutOutline = 4;

    // Enemy moves ("COUNTER-ATTACK", "ENEMY LANDING", "SITE LOST") are announced larger and linger.
    private const float AlertSeconds = 4;
    private const float AlertHeight = 16;
    private const int AlertFontSize = 24;

    /// <summary>Threatened site labels pulse at this rate, in cycles per second.</summary>
    private const float ThreatPulseRate = 1.5f;

    /// <summary>Callouts born within this many metres of a live one stack above it instead of overlapping.</summary>
    private const float CalloutStackRadius = 20;
    private const float CalloutLineHeight = 18;
    private const int CalloutStackLimit = 3;

    private readonly Dictionary<int, float> damageUntil = [];
    private readonly List<Callout> callouts = [];
    private Simulation? sim;
    private Camera3D? camera;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Report(CombatEvent evt, float time)
    {
        if (evt.PlayerVisible && evt.UnitId >= 0 && evt.Kind is CombatEventKind.Impact or CombatEventKind.Destroy)
        {
            damageUntil[evt.UnitId] = time + HitRingSeconds;
        }

        if (evt.PlayerVisible && AlertText(evt.Kind) is string alert)
        {
            callouts.Add(new(evt.Position, alert, Palette.Ui.Alert, AlertFontSize, time, 0, AlertSeconds, AlertHeight));
        }

        if (evt.PlayerVisible && sim is { Classic: true } && ShotResult(sim, evt) is var (result, resultColor))
        {
            AddCallout(evt, result, resultColor, time);
            return;
        }

        if (evt.PlayerVisible && evt.Armor is ArmorFacing facing)
        {
            var (text, color) = evt.Kind == CombatEventKind.Destroy
                ? ("KNOCKED OUT", Palette.Ui.KnockedOut)
                : facing switch
                {
                    ArmorFacing.Front => ("RICOCHET", Palette.Ui.Ricochet),
                    ArmorFacing.Side => ("FLANK HIT", Palette.Ui.FlankHit),
                    _ => ("REAR HIT", Palette.Ui.RearHit),
                };
            AddCallout(evt, text, color, time);
        }
    }

    /// <summary>
    /// A Classic shell's result, as the player hears it from the gunner: HIT with the target and the
    /// damage, KILL with the bounty, MISS; and how hard the player's own vehicles are hit. Null for
    /// anything else.
    /// </summary>
    public static (string Text, Color Color)? ShotResult(Simulation sim, CombatEvent evt)
    {
        if (evt.Shooter < 0 || evt.Kind is not (CombatEventKind.Impact or CombatEventKind.Destroy))
        {
            return null;
        }

        var shooter = sim.Vehicles.FirstOrDefault(v => v.Id == evt.Shooter);
        var victim = sim.Vehicles.FirstOrDefault(v => v.Id == evt.UnitId);
        if (shooter is { Enemy: false })
        {
            return victim switch
            {
                null => ("MISS", Palette.Ui.Ricochet),
                { Enemy: false } => ($"FRIENDLY FIRE {evt.Damage:0}%", Palette.Ui.KnockedOut),
                _ when evt.Kind == CombatEventKind.Destroy => ($"KILL {victim.Spec.Name}  +{Campaign.Money(Campaign.Price(victim.Kind), GameMode.Classic)}", Palette.Ui.KnockedOut),
                _ => ($"HIT {victim.Spec.Name} {evt.Damage:0}%", Palette.Ui.FlankHit),
            };
        }

        return victim is { Enemy: false } ? ($"{victim.Spec.Name} HIT {evt.Damage:0}%", Palette.Ui.RearHit) : null;
    }

    private void AddCallout(CombatEvent evt, string text, Color color, float time)
    {
        var nearby = callouts.Where(c => c.Seconds == CalloutSeconds && V2.Distance(c.Position, evt.Position) < CalloutStackRadius && time - c.Time < CalloutSeconds).ToList();
        int slot;
        while ((slot = Enumerable.Range(0, CalloutStackLimit).FirstOrDefault(s => nearby.All(c => c.Slot != s), -1)) < 0)
        {
            // A heavy exchange of fire: the newest callout replaces the oldest rather than stacking higher.
            var oldest = nearby.OrderBy(c => c.Time).First();
            callouts.Remove(oldest);
            nearby.Remove(oldest);
        }

        callouts.Add(new(evt.Position, text, color, evt.Kind == CombatEventKind.Destroy ? KillFontSize : CalloutFontSize, time, slot, CalloutSeconds, CalloutHeight));
    }

    public void Track(Simulation simulation, Camera3D view)
    {
        if (sim != simulation)
        {
            damageUntil.Clear();
            callouts.Clear();
        }

        sim = simulation;
        camera = view;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (sim == null || camera == null || sim.Phase == MissionPhase.Briefing)
        {
            return;
        }

        foreach (var v in sim.Vehicles.Where(v => v.Alive && sim.Recon.IsVisible(v)))
        {
            if (Project(BattleView.World(v.Position, VehicleLabelHeight)) is not Vector2 p)
            {
                continue;
            }

            var color = Palette.Team(v);
            var origin = p - new Vector2(HealthBarWidth / 2, 0);
            DrawRect(new Rect2(origin, new(HealthBarWidth, HealthBarHeight)), Palette.Ui.HealthTrack);
            DrawRect(new Rect2(origin, new(HealthBarWidth * v.Health / Vehicle.MaxHealth, HealthBarHeight)), color);
            if (damageUntil.TryGetValue(v.Id, out float until) && until > sim.Elapsed)
            {
                DrawCircle(p, HitRingRadius, Palette.Ui.HitRing with { A = Math.Min(1, until - sim.Elapsed) }, false, 2, true);
            }
        }

        for (int i = 0; i < sim.Sites.Count; i++)
        {
            var site = sim.Sites[i];
            if (Project(BattleView.World(site.Position, SiteLabelHeight)) is Vector2 p)
            {
                string state = site.Secured ? "SECURE" : site.Contested ? $"{site.Fraction:0%}  CONTESTED" : $"{site.Fraction:0%}";
                var color = site.Contested ? Palette.Enemy : Palette.Ui.Ink;
                if (sim.Threatened(i))
                {
                    state += "  ENEMY INBOUND";
                    color = color.Lerp(Palette.Ui.Alert, .5f + .5f * Mathf.Sin(sim.Elapsed * ThreatPulseRate * Mathf.Tau));
                }

                DrawString(UiFonts.Callout, p + SiteLabelOffset, $"{i + 1:00}  {state}", HorizontalAlignment.Left, -1, SiteFontSize, color);
            }
        }

        foreach (var contact in sim.Recon.Ghosts)
        {
            if (Project(BattleView.World(contact.Position, GhostLabelHeight)) is Vector2 p)
            {
                DrawCircle(p, GhostRadius, Palette.Ghost, false, 1.5f, true);
                DrawString(UiFonts.Callout, p + GhostLabelOffset, $"? {(int)(sim.Elapsed - contact.LastSeen)}s", HorizontalAlignment.Left, -1, GhostFontSize, Palette.Ghost);
            }
        }

        DrawCallouts(sim.Elapsed);
    }

    private void DrawCallouts(float now)
    {
        callouts.RemoveAll(c => now - c.Time > c.Seconds || now < c.Time);
        var font = UiFonts.Callout;
        foreach (var callout in callouts)
        {
            if (Project(BattleView.World(callout.Position, callout.Height)) is not Vector2 p)
            {
                continue;
            }

            float age = (now - callout.Time) / callout.Seconds;
            // Rise quickly, then hang; fade out over the second half.
            var at = p - new Vector2(font.GetStringSize(callout.Text, HorizontalAlignment.Left, -1, callout.Size).X / 2, CalloutRise * Mathf.Sqrt(age) + CalloutLineHeight * callout.Slot);
            float alpha = Math.Clamp(2 - age * 2, 0, 1);
            DrawStringOutline(font, at, callout.Text, HorizontalAlignment.Left, -1, callout.Size, CalloutOutline, Palette.Ui.CalloutOutline with { A = alpha });
            DrawString(font, at, callout.Text, HorizontalAlignment.Left, -1, callout.Size, callout.Color with { A = alpha });
        }
    }

    private static string? AlertText(CombatEventKind kind) => kind switch
    {
        CombatEventKind.Assault => "ENEMY INBOUND",
        CombatEventKind.Reinforcement => "ENEMY LANDING",
        CombatEventKind.SiteLost => "SITE LOST",
        CombatEventKind.IncomingArtillery => "INCOMING",
        _ => null,
    };

    private Vector2? Project(Vector3 world) => camera == null || camera.IsPositionBehind(world) ? null : camera.UnprojectPosition(world);

    private sealed record Callout(V2 Position, string Text, Color Color, int Size, float Time, int Slot, float Seconds, float Height);
}
