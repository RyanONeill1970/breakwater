namespace Breakwater.Core;

using System.Numerics;

public sealed class Vehicle
{
    /// <summary>Health is a percentage.</summary>
    public const float MaxHealth = 100;

    /// <summary>Queued destination orders per vehicle (Shift + right-click).</summary>
    public const int MaxOrders = 8;

    /// <summary>
    /// Radius for shell hits and line-of-fire occlusion. Slightly inside the collision radius
    /// (<see cref="Mission.VehicleRadius"/>) so a shell grazing the hull edge still passes.
    /// </summary>
    public const float HitRadius = 1.8f;

    /// <summary><see cref="Objective"/> value for a defender with no site to attack.</summary>
    public const int NoObjective = -1;

    public int Id { get; set; }

    public bool Alive => Health > 0;

    /// <summary>A supply truck in a Classic escort battle: friendly, but outside the player's four groups.</summary>
    public bool Convoy => Group == Mission.ConvoyGroup;

    /// <summary>One of the player's own sixteen roster vehicles.</summary>
    public bool Player => !Enemy && !Convoy;
    public int Ammo { get; set; }
    public bool Enemy { get; set; }
    public int Group { get; set; }
    public float Heading { get; set; }
    public float Health { get; set; } = MaxHealth;
    public bool HoldFire { get; set; }
    public Vector2 Home { get; set; }
    public VehicleKind Kind { get; set; }

    /// <summary>Classic: the ammunition in the breech. Switching type means reloading.</summary>
    public Weapon Loaded { get; set; } = Weapon.MachineGun;

    /// <summary>Index of the capture site a defender is sent to retake or relieve, or <see cref="NoObjective"/>.</summary>
    public int Objective { get; set; } = NoObjective;
    public List<Vector2> Orders { get; set; } = [];
    public int PatrolLeg { get; set; }
    public Vector2 Position { get; set; }
    public float Reload { get; set; }

    /// <summary>Classic: seconds until this vehicle, having fired, can hide in concealing terrain again.</summary>
    public float Revealed { get; set; }

    /// <summary>Classic: rounds left of each ammunition type, indexed by <see cref="Weapon"/>. Empty for Modern kinds.</summary>
    public int[] Rounds { get; set; } = [];

    /// <summary>Main-gun rounds left: <see cref="Ammo"/> for Modern kinds, the sum of <see cref="Rounds"/> for Classic ones.</summary>
    public int RoundsLeft => Spec.Classic ? Rounds.Sum() : Ammo;

    public List<Vector2> Route { get; set; } = [];

    /// <summary>Classic smoke grenades left.</summary>
    public int Smoke { get; set; }

    public VehicleSpec Spec => VehicleSpec.For(Kind);
    public float Speed { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Ready;
    public float Stuck { get; set; }
    public Tactic Tactic { get; set; }
    public float Think { get; set; }
    public float TurretHeading { get; set; }

    /// <summary>Classic garrison fire discipline: holding fire until provoked. Unlike <see cref="HoldFire"/>, the AI lifts it.</summary>
    public bool Waiting { get; set; }

    /// <summary>Whether this vehicle has <paramref name="weapon"/> and a round to fire from it.</summary>
    public bool CanFire(Weapon weapon) =>
        Spec.Carries(weapon) && (weapon == Weapon.MachineGun || Rounds[(int)weapon] > 0);

    /// <summary>Fills the vehicle's gun or magazine to its full load, with its heaviest round in the breech.</summary>
    public void Rearm()
    {
        Ammo = Spec.Ammo;
        Rounds = [.. Spec.Magazine];
        Smoke = Spec.SmokeRounds;
        Loaded = ClassicRules.Preference(VehicleKind.Abrams).FirstOrDefault(CanFire, Weapon.MachineGun);
    }
}
