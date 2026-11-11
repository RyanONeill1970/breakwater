namespace Breakwater;

using V2 = System.Numerics.Vector2;

/// <summary>
/// The tactical (orthographic, top-down) and chase (perspective, behind the leader) cameras, and in
/// Classic mode the gunner sight (first person, along the leader's gun).
/// </summary>
public partial class CameraRig : Node3D
{
    // Tactical view. Zoom is the orthographic height of the view, in metres.
    private const float DefaultZoom = 210;
    private const float MinZoom = 65;
    private const float MaxZoom = 250;
    private const float ZoomStep = 12;
    private static readonly Vector3 TacticalOffset = new(0, 195, 95);

    /// <summary>The focus may pan to within this distance of the island's edge.</summary>
    private const float FocusLimit = Mission.Edge - 2;

    /// <summary>Screen widths per second at pan speed 1, scaled by zoom.</summary>
    private const float KeyPanRate = .5f;

    /// <summary>Converts mouse-drag pixels to metres per unit of zoom.</summary>
    private const float DragScale = 1f / 900;

    // Chase view.
    private const float ChaseFov = 62;
    private const float ChaseDistance = 16;
    private const float ChaseHeight = 10;
    private const float ChaseLookAhead = 4;
    private const float ChaseLookHeight = 1.5f;

    /// <summary>Exponential smoothing rate for the chase camera; higher follows more tightly.</summary>
    private const float ChaseSmoothing = 7;

    // Gunner sight: the eye sits above the turret roof, a little forward, and looks along the gun.
    private const float SightFov = 48;
    public const float SightMagnification = 6;
    private const float SightHeight = 2.35f;
    private const float SightForward = .6f;
    private const float SightLookDistance = 100;

    /// <summary>Shake in the sight, as a fraction of the chase view's: the gunner is braced against the optic.</summary>
    private const float SightShake = .15f;

    // Camera shake. Events add trauma (0–1), which decays; the offset grows with trauma squared, so
    // small knocks stay subtle and a nearby kill really jolts.
    private const float ShotTrauma = .1f;
    private const float HitTrauma = .22f;
    private const float KillTrauma = .6f;
    private const float TraumaDecay = 1.4f;
    private const float ShakeFrequency = 2.5f;

    /// <summary>Full-trauma offset: metres in the chase view, a fraction of the zoom in the tactical view.</summary>
    private const float ChaseShake = 1.2f, TacticalShake = .02f;

    /// <summary>Events within this distance of the view shake it fully; beyond it, not at all (chase view, metres).</summary>
    private const float ChaseShakeNear = 12, ChaseShakeFar = 70;

    /// <summary>As above for the tactical view, as a fraction of the zoom.</summary>
    private const float TacticalShakeNear = .15f, TacticalShakeFar = .5f;

    /// <summary>The noise channels for the three shake axes, far enough apart to be uncorrelated.</summary>
    private const float ShakeChannel = 100;

    private const float FarPlane = 1500;

    /// <summary>A view ray this close to horizontal never meets the ground usefully.</summary>
    private const float HorizontalRay = .0001f;

    private readonly FastNoiseLite noise = new() { Frequency = ShakeFrequency };
    private Vector3 focus;
    private Vector3 chase;
    private float trauma;
    private float shakeTime;

    public Camera3D Camera { get; } = new() { Current = true, Far = FarPlane };

    public Vector3 Focus => focus;

    /// <summary>With <see cref="Tactical"/> off: look through the leader's gun sight rather than chase it.</summary>
    public bool Sight { get; set; }

    /// <summary>The sight's high magnification (<see cref="SightMagnification"/>×) is selected.</summary>
    public bool SightZoomed { get; set; }

    /// <summary>The battle is at night, so the sight shows the thermal picture.</summary>
    public bool Night { get; set; }

    public bool Tactical { get; set; } = true;

    public float Zoom { get; private set; } = DefaultZoom;

    public override void _Ready() => AddChild(Camera);

    public void Reset()
    {
        focus = Vector3.Zero;
        trauma = 0;
        Night = false;
        Camera.Environment = null;
        Sight = false;
        SightZoomed = false;
        Tactical = true;
        Zoom = DefaultZoom;
    }

    public void ZoomIn() => Zoom = Math.Max(MinZoom, Zoom - ZoomStep);

    public void ZoomOut() => Zoom = Math.Min(MaxZoom, Zoom + ZoomStep);

    public void FocusOn(V2 point) => SetFocus(BattleView.World(point));

    /// <summary>Keyboard pan; <paramref name="direction"/> components are −1…1.</summary>
    public void Pan(Vector2 direction, float dt, float speed) =>
        SetFocus(focus + new Vector3(direction.X, 0, direction.Y) * dt * Zoom * KeyPanRate * speed);

    public void Drag(Vector2 relative, float speed) =>
        SetFocus(focus + new Vector3(-relative.X, 0, -relative.Y) * Zoom * DragScale * speed);

    /// <summary>Jolts the view for a visible shot, hit or kill, less the farther it is from what the camera watches.</summary>
    public void Shake(CombatEvent evt, Vehicle? leader)
    {
        float strength = evt.Kind switch
        {
            CombatEventKind.Shot => ShotTrauma,
            CombatEventKind.Impact => HitTrauma,
            CombatEventKind.Destroy => KillTrauma,
            _ => 0,
        };
        if (!evt.PlayerVisible || strength == 0)
        {
            return;
        }

        bool tactical = Tactical || leader == null;
        var watched = Tactical || leader == null ? focus : BattleView.World(leader.Position);
        float near = tactical ? Zoom * TacticalShakeNear : ChaseShakeNear;
        float far = tactical ? Zoom * TacticalShakeFar : ChaseShakeFar;
        float distance = watched.DistanceTo(BattleView.World(evt.Position));
        float falloff = 1 - Math.Clamp((distance - near) / (far - near), 0, 1);
        trauma = Math.Min(1, trauma + strength * falloff);
    }

    public void Update(float dt, Vehicle? leader)
    {
        // Thermal belongs only to an active night sight, never to tactical/chase or a lost group.
        Camera.Environment = null;
        trauma = Math.Max(0, trauma - dt * TraumaDecay);
        shakeTime += dt;
        float amplitude = trauma * trauma * (Tactical || leader == null ? Zoom * TacticalShake : ChaseShake);
        var shake = amplitude * new Vector3(noise.GetNoise1D(shakeTime), noise.GetNoise1D(shakeTime + ShakeChannel), noise.GetNoise1D(shakeTime + ShakeChannel * 2));

        if (Tactical || leader == null)
        {
            Camera.Projection = Camera3D.ProjectionType.Orthogonal;
            Camera.Size = Zoom;
            chase = focus + TacticalOffset;
            Camera.Position = chase + shake;
            Camera.LookAt(focus + shake);
            return;
        }

        Camera.Projection = Camera3D.ProjectionType.Perspective;
        if (Sight)
        {
            AimSight(Camera, leader, SightZoomed, Night, shake * SightShake);
            chase = Camera.Position;
            return;
        }

        Camera.Fov = ChaseFov;
        var forward = BattleView.World(Geometry.Forward(leader.Heading));
        var desired = BattleView.World(leader.Position) - forward * ChaseDistance + Vector3.Up * ChaseHeight;
        chase = chase.Lerp(desired, 1 - Mathf.Exp(-dt * ChaseSmoothing));
        Camera.Position = chase + shake;
        // The aim point shakes less than the eye, so the view kicks and tilts rather than just sliding.
        Camera.LookAt(BattleView.World(leader.Position, ChaseLookHeight) + forward * ChaseLookAhead + shake / 2);
    }

    // The thermal sight: flat bright lighting, drained of colour and mapped onto a green ramp.
    private const float ThermalAmbient = .45f, ThermalBrightness = 1.3f, ThermalContrast = 1.3f;

    /// <summary>Shared by every sight camera at night; null by day.</summary>
    private static Godot.Environment? thermal;

    /// <summary>Puts <paramref name="camera"/> at <paramref name="leader"/>'s gun sight, looking along the gun, jolted by <paramref name="shake"/>.</summary>
    /// <remarks>At <paramref name="night"/> the camera sees through the thermal sight's filter.</remarks>
    public static void AimSight(Camera3D camera, Vehicle leader, bool zoomed, bool night, Vector3 shake = default)
    {
        camera.Environment = night ? Thermal() : null;
        var gun = BattleView.World(Geometry.Forward(leader.TurretHeading));
        var eye = BattleView.World(leader.Position, SightHeight) + gun * SightForward;
        camera.Projection = Camera3D.ProjectionType.Perspective;
        camera.Fov = zoomed ? SightFov / SightMagnification : SightFov;
        camera.Position = eye + shake;
        camera.LookAt(eye + gun * SightLookDistance + shake);
    }

    private static Godot.Environment Thermal() => thermal ??= new Godot.Environment
    {
        AdjustmentBrightness = ThermalBrightness,
        AdjustmentColorCorrection = new GradientTexture1D
        {
            Gradient = new Gradient { Colors = [Palette.Night.ThermalCold, Palette.Night.ThermalHot], Offsets = [0, 1] },
        },
        AdjustmentContrast = ThermalContrast,
        AdjustmentEnabled = true,
        AdjustmentSaturation = 0,
        AmbientLightColor = Colors.White,
        AmbientLightEnergy = ThermalAmbient,
        AmbientLightSource = Godot.Environment.AmbientSource.Color,
        BackgroundColor = Palette.Night.ThermalCold,
        BackgroundMode = Godot.Environment.BGMode.Color,
    };

    /// <summary>Where a screen point meets the ground plane, or null if the ray never does.</summary>
    public V2? GroundPoint(Vector2 screen)
    {
        var from = Camera.ProjectRayOrigin(screen);
        var direction = Camera.ProjectRayNormal(screen);
        if (Math.Abs(direction.Y) < HorizontalRay)
        {
            return null;
        }

        float distance = -from.Y / direction.Y;
        if (distance < 0)
        {
            return null;
        }

        var point = from + direction * distance;
        return new V2(point.X, point.Z);
    }

    private void SetFocus(Vector3 value)
    {
        focus = new(Math.Clamp(value.X, -FocusLimit, FocusLimit), 0, Math.Clamp(value.Z, -FocusLimit, FocusLimit));
    }
}
