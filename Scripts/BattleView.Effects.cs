namespace Breakwater;



/// <summary>
/// Combat effects: muzzle flashes, hit sparks, explosions, damage smoke and burning wrecks, all
/// built from CPU particles so they run on the Compatibility renderer. Particle property values
/// are tuning, named by the property they set.
/// </summary>
public partial class BattleView
{
    // Heights above the ground, in metres.
    private const float HitHeight = 1.3f;
    private const float BlastHeight = 1.2f;
    private const float ScorchHeight = .04f;
    private const float WreckFireHeight = 1.6f;



    /// <summary>Vehicles below this health smoke.</summary>
    private const float SmokeBelowHealth = 40;

    /// <summary>A wreck's fire burns this long, in simulation seconds, then only smoulders.</summary>
    private const float WreckBurnSeconds = 40;

    // A knocked-out tank's turret is lifted and thrown askew by the blast.
    private static readonly Vector3 WreckTurretLift = new(.25f, .3f, -.2f);
    private static readonly Vector3 WreckTurretTilt = new(-14, 23, 9);

    private const float ScorchRadius = 3.4f;

    private readonly Dictionary<int, CpuParticles3D> damageSmoke = [];
    private readonly Dictionary<int, (CpuParticles3D Fire, float Until)> wreckFires = [];

    // Shared particle resources, created once per view.
    private readonly SphereMesh puff = Sphere(.5f);
    private readonly SphereMesh ball = Sphere(1);
    private readonly BoxMesh chip = new() { Size = new(.09f, .09f, .09f) };
    private readonly BoxMesh chunk = new() { Size = new(.5f, .3f, .7f) };
    private readonly TorusMesh shockRing = new() { InnerRadius = .92f, OuterRadius = 1, RingSegments = 4, Rings = 36 };

    // Flashes and sparks add light; smoke and flames mix, so fire stays orange over bright sand instead of blowing out to white.
    private readonly StandardMaterial3D additive = ParticleMaterial(BaseMaterial3D.BlendModeEnum.Add);
    private readonly StandardMaterial3D mixed = ParticleMaterial(BaseMaterial3D.BlendModeEnum.Mix);
    private readonly StandardMaterial3D debris = new() { AlbedoColor = Palette.World.Debris, Roughness = .9f };

    private readonly Gradient flashRamp = Ramp((0, Palette.World.FlashCore), (.4f, Palette.World.Flash), (1, Palette.World.Flash with { A = 0 }));
    private readonly Gradient fireRamp = Ramp((0, Palette.World.Spark), (.2f, Palette.World.Fire), (.55f, Palette.World.FireDeep), (1, Palette.World.FireDeep with { A = 0 }));
    private readonly Gradient smokeRamp = Ramp((0, Palette.World.SmokeDark with { A = 0 }), (.12f, Palette.World.SmokeDark), (1, Palette.World.SmokeLight with { A = 0 }));
    private readonly Gradient greySmokeRamp = Ramp((0, Palette.World.SmokeLight with { A = 0 }), (.15f, Palette.World.SmokeLight), (1, Palette.World.SmokeLight with { A = 0 }));
    private readonly Gradient sparkRamp = Ramp((0, Palette.World.Spark), (.5f, Palette.World.Fire), (1, Palette.World.FireDeep with { A = 0 }));
    private readonly Gradient dustRamp = Ramp((0, Palette.World.Dust), (1, Palette.World.Dust with { A = 0 }));
    private readonly Gradient shockRamp = Ramp((0, Palette.World.Shockwave), (1, Palette.World.Shockwave with { A = 0 }));
    private readonly Curve grow = Line(.35f, 1);
    private readonly Curve swell = Line(.2f, 1);
    private readonly Curve shrink = Line(1, .15f);

    public void Effect(CombatEvent evt)
    {
        if (!evt.PlayerVisible)
        {
            return;
        }

        switch (evt.Kind)
        {
            case CombatEventKind.Shot:
                MuzzleBlast(evt.UnitId);
                break;
            case CombatEventKind.Impact when evt.Armor.HasValue:
                HitSparks(World(evt.Position, HitHeight), evt.Armor == ArmorFacing.Front);
                break;
            case CombatEventKind.Impact:
                Dust(World(evt.Position, HitHeight));
                break;
            case CombatEventKind.Destroy:
                Explosion(World(evt.Position, BlastHeight));
                break;
            case CombatEventKind.Barrage or CombatEventKind.MineBlast or CombatEventKind.BuildingDestroyed:
                Explosion(World(evt.Position, ScorchHeight));
                Dust(World(evt.Position, HitHeight));
                break;
            case CombatEventKind.Capture:
                CaptureBurst(World(evt.Position));
                break;
        }
    }

    private static StandardMaterial3D ParticleMaterial(BaseMaterial3D.BlendModeEnum blend) => new()
    {
        BlendMode = blend,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
    };

    private static Gradient Ramp(params (float Offset, Color Color)[] stops) => new()
    {
        Colors = [.. stops.Select(s => s.Color)],
        Offsets = [.. stops.Select(s => s.Offset)],
    };

    private static Curve Line(float from, float to)
    {
        var curve = new Curve();
        curve.AddPoint(new(0, from));
        curve.AddPoint(new(1, to));
        return curve;
    }

    /// <summary>
    /// A one-shot burst that frees itself when its last particle dies. It starts at the end of the
    /// frame, once the caller has finished tuning it: changing the amount of a running one-shot
    /// emitter ends it.
    /// </summary>
    private CpuParticles3D Burst(Vector3 at, Mesh mesh, Material material, Gradient? ramp)
    {
        var burst = new CpuParticles3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ColorRamp = ramp,
            Emitting = false,
            Explosiveness = 1,
            MaterialOverride = material,
            Mesh = mesh,
            OneShot = true,
            Position = at,
        };
        burst.Finished += burst.QueueFree;
        AddChild(burst);
        burst.CallDeferred(CpuParticles3D.MethodName.Restart);
        return burst;
    }

    private void MuzzleBlast(int unitId)
    {
        if (!models.TryGetValue(unitId, out var model))
        {
            return;
        }

        var at = model.Turret.GlobalTransform * model.Muzzle;
        var forward = -model.Turret.GlobalBasis.Z;
        var flash = Burst(at, puff, additive, flashRamp);
        flash.Amount = 6;
        flash.Direction = forward;
        flash.InitialVelocityMin = 4;
        flash.InitialVelocityMax = 9;
        flash.Lifetime = .16f;
        flash.ScaleAmountCurve = grow;
        flash.Spread = 18;
        flash.Gravity = Vector3.Zero;

        var smoke = Burst(at, puff, mixed, greySmokeRamp);
        smoke.Amount = 5;
        smoke.DampingMin = 3;
        smoke.DampingMax = 5;
        smoke.Direction = forward;
        smoke.Gravity = Vector3.Up * .6f;
        smoke.InitialVelocityMin = 2;
        smoke.InitialVelocityMax = 5;
        smoke.Lifetime = 1.2f;
        smoke.ScaleAmountCurve = swell;
        smoke.ScaleAmountMax = 2.4f;
        smoke.ScaleAmountMin = 1.4f;
        smoke.Spread = 25;
    }

    private void HitSparks(Vector3 at, bool ricochet)
    {
        var sparks = Burst(at, chip, additive, sparkRamp);
        // A ricochet throws more, faster sparks.
        sparks.Amount = ricochet ? 22 : 12;
        sparks.Direction = Vector3.Up;
        sparks.Gravity = Vector3.Down * 18;
        sparks.InitialVelocityMin = 5;
        sparks.InitialVelocityMax = ricochet ? 16 : 10;
        sparks.Lifetime = .55f;
        sparks.Spread = 80;

        var flash = Burst(at, puff, additive, flashRamp);
        flash.Amount = 1;
        flash.Gravity = Vector3.Zero;
        flash.InitialVelocityMax = 0;
        flash.Lifetime = .18f;
        flash.ScaleAmountCurve = grow;
        flash.ScaleAmountMin = flash.ScaleAmountMax = 2;
    }

    private void Dust(Vector3 at)
    {
        var dust = Burst(at, puff, mixed, dustRamp);
        dust.Amount = 9;
        dust.DampingMin = dust.DampingMax = 4;
        dust.Direction = Vector3.Up;
        dust.Gravity = Vector3.Down * 2;
        dust.InitialVelocityMin = 2;
        dust.InitialVelocityMax = 6;
        dust.Lifetime = .9f;
        dust.ScaleAmountCurve = swell;
        dust.ScaleAmountMin = 1;
        dust.ScaleAmountMax = 2.2f;
        dust.Spread = 55;
    }

    private void Explosion(Vector3 at)
    {
        var flash = Burst(at, ball, additive, flashRamp);
        flash.Amount = 1;
        flash.Gravity = Vector3.Zero;
        flash.InitialVelocityMax = 0;
        flash.Lifetime = .3f;
        flash.ScaleAmountCurve = grow;
        flash.ScaleAmountMin = flash.ScaleAmountMax = 4;

        var fireball = Burst(at, ball, mixed, fireRamp);
        fireball.Amount = 24;
        fireball.DampingMin = fireball.DampingMax = 5;
        fireball.Direction = Vector3.Up;
        fireball.EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere;
        fireball.EmissionSphereRadius = 1.6f;
        fireball.Gravity = Vector3.Up * 3;
        fireball.InitialVelocityMin = 3;
        fireball.InitialVelocityMax = 8;
        fireball.Lifetime = 1.4f;
        fireball.ScaleAmountCurve = swell;
        fireball.ScaleAmountMin = 1.8f;
        fireball.ScaleAmountMax = 3.4f;
        fireball.Spread = 70;

        var smoke = Burst(at + Vector3.Up, ball, mixed, smokeRamp);
        smoke.Amount = 16;
        smoke.DampingMin = smoke.DampingMax = 2;
        smoke.Direction = Vector3.Up;
        smoke.EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere;
        smoke.EmissionSphereRadius = 1.5f;
        smoke.Gravity = Vector3.Up * 1.5f;
        smoke.InitialVelocityMin = 2;
        smoke.InitialVelocityMax = 5;
        smoke.Lifetime = 3.2f;
        smoke.ScaleAmountCurve = swell;
        smoke.ScaleAmountMin = 1.5f;
        smoke.ScaleAmountMax = 3;
        smoke.Spread = 40;

        var fragments = Burst(at, chunk, debris, null);
        fragments.AngularVelocityMin = -540;
        fragments.AngularVelocityMax = 540;
        fragments.Amount = 14;
        fragments.Direction = Vector3.Up;
        fragments.Gravity = Vector3.Down * 24;
        fragments.InitialVelocityMin = 9;
        fragments.InitialVelocityMax = 18;
        fragments.Lifetime = 1.5f;
        fragments.ScaleAmountMin = .6f;
        fragments.ScaleAmountMax = 1.4f;
        fragments.Spread = 55;

        var sparks = Burst(at, chip, additive, sparkRamp);
        sparks.Amount = 40;
        sparks.Direction = Vector3.Up;
        sparks.Gravity = Vector3.Down * 14;
        sparks.InitialVelocityMin = 10;
        sparks.InitialVelocityMax = 24;
        sparks.Lifetime = 1.1f;
        sparks.Spread = 85;

        var shock = Burst(new(at.X, ScorchHeight * 3, at.Z), shockRing, additive, shockRamp);
        shock.Amount = 1;
        shock.Gravity = Vector3.Zero;
        shock.InitialVelocityMax = 0;
        shock.Lifetime = .5f;
        shock.ScaleAmountCurve = Line(.1f, 1);
        shock.ScaleAmountMin = shock.ScaleAmountMax = 14;
    }

    /// <summary>Blackens the hull, knocks the turret askew, scorches the ground and sets the wreck burning.</summary>
    private void MakeWreck(Vehicle tank, Model model, float now)
    {
        var wreck = Material(Palette.World.Wreck);
        foreach (var mesh in model.Hull.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            mesh.MaterialOverride = wreck;
        }

        model.Marker.Visible = false;
        model.Turret.Position += WreckTurretLift;
        model.Turret.RotationDegrees += WreckTurretTilt;

        var scorch = Mesh(model.Hull, new(0, ScorchHeight, 0), new CylinderMesh { BottomRadius = ScorchRadius, Height = .02f, RadialSegments = 20, TopRadius = ScorchRadius }, Palette.World.Scorch);
        scorch.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        var fire = new CpuParticles3D
        {
            Amount = 18,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ColorRamp = fireRamp,
            Direction = Vector3.Up,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = .9f,
            Gravity = Vector3.Up * 2,
            InitialVelocityMax = 2.5f,
            InitialVelocityMin = 1,
            Lifetime = .8f,
            MaterialOverride = mixed,
            Mesh = puff,
            Position = Vector3.Up * WreckFireHeight,
            ScaleAmountCurve = shrink,
            ScaleAmountMax = 2.2f,
            ScaleAmountMin = 1.2f,
            Spread = 20,
        };
        model.Hull.AddChild(fire);
        wreckFires[tank.Id] = (fire, now + WreckBurnSeconds);
        SetSmoke(tank.Id, model, true);
    }

    private void SyncDamage(Simulation sim, Vehicle tank, Model model)
    {
        if (tank.Alive && tank.Health < SmokeBelowHealth && !damageSmoke.ContainsKey(tank.Id))
        {
            SetSmoke(tank.Id, model, false);
        }

        if (wreckFires.TryGetValue(tank.Id, out var burning) && sim.Elapsed > burning.Until && burning.Fire.Emitting)
        {
            burning.Fire.Emitting = false;
        }
    }

    /// <summary>A rising smoke trail: grey and thin from a damaged vehicle, a thick black column from a wreck.</summary>
    private void SetSmoke(int id, Model model, bool wreck)
    {
        if (damageSmoke.Remove(id, out var old))
        {
            old.QueueFree();
        }

        var smoke = new CpuParticles3D
        {
            Amount = wreck ? 26 : 12,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ColorRamp = wreck ? smokeRamp : greySmokeRamp,
            Direction = Vector3.Up,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = .6f,
            // A light breeze from the sea bends every column the same way.
            Gravity = new Vector3(.9f, 1.2f, .4f),
            InitialVelocityMax = 3,
            InitialVelocityMin = 1.5f,
            Lifetime = wreck ? 5.5f : 3,
            MaterialOverride = mixed,
            Mesh = puff,
            Position = Vector3.Up * (WreckFireHeight + .6f),
            ScaleAmountCurve = swell,
            ScaleAmountMax = wreck ? 4.5f : 2.6f,
            ScaleAmountMin = wreck ? 2.5f : 1.4f,
            Spread = 12,
        };
        model.Hull.AddChild(smoke);
        damageSmoke[id] = smoke;
    }

}
