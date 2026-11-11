namespace Breakwater.Core;

// The gunner sight (Classic mode): the player lays one vehicle's gun by hand, picks its ammunition
// and fires where it points. That vehicle's turret stops tracking targets by itself; everything
// else about it (movement, reloads, damage) runs as usual.
public sealed partial class Simulation
{
    /// <summary>The rangefinder reads no farther than this, in world metres.</summary>
    public static readonly float RangefinderLimit = ClassicRules.Distance(4000);

    /// <summary>The vehicle whose gun the player is laying, or −1. Presentation state: not saved.</summary>
    public int GunnerId { get; set; } = -1;

    /// <summary>Turret traverse the player is asking for on <see cref="GunnerId"/>'s vehicle, −1 (right) … 1 (left).</summary>
    public float GunnerTraverse { get; set; }

    /// <summary>What lies along <paramref name="tank"/>'s gun: the distance to the first hull or cover it meets, and the hull if it is one.</summary>
    public (float Distance, Vehicle? Vehicle)? RangeAlongGun(Vehicle tank)
    {
        var end = tank.Position + Geometry.Forward(tank.TurretHeading) * RangefinderLimit;
        float nearest = float.PositiveInfinity;
        Vehicle? hit = null;
        foreach (var obstacle in Obstacles)
        {
            if (Geometry.HitBox(tank.Position, end, obstacle) is float t && t < nearest)
            {
                nearest = t;
                hit = null;
            }
        }

        foreach (var v in Vehicles.Where(v => v != tank))
        {
            if (Geometry.HitCircle(tank.Position, end, v.Position, Vehicle.HitRadius) is float t && t < nearest)
            {
                nearest = t;
                hit = v;
            }
        }

        return float.IsFinite(nearest) ? (nearest * RangefinderLimit, hit) : null;
    }

    /// <summary>
    /// Loads the next ammunition type <paramref name="tank"/> has rounds for, starting that type's
    /// reload. Returns the new type, or null if there is nothing else to load.
    /// </summary>
    public Weapon? CycleAmmunition(Vehicle tank)
    {
        if (!tank.Alive || !tank.Spec.Classic)
        {
            return null;
        }

        var weapons = Enum.GetValues<Weapon>();
        for (int step = 1; step < weapons.Length; step++)
        {
            var next = weapons[((int)tank.Loaded + step) % weapons.Length];
            if (tank.CanFire(next))
            {
                tank.Loaded = next;
                tank.Reload = WeaponSpec.For(next).Reload;
                return next;
            }
        }

        return null;
    }

    /// <summary>
    /// Fires <paramref name="tank"/>'s loaded round straight along its gun, whether or not anything is
    /// there. The shot scatters as any Classic shot does, against whatever the gun is laid on.
    /// </summary>
    public bool FireGunner(Vehicle tank)
    {
        var weapon = WeaponSpec.For(tank.Loaded);
        if (Phase != MissionPhase.Active || Paused || !tank.Alive || !tank.Spec.Classic || tank.Reload > 0 || !tank.CanFire(tank.Loaded) ||
            (weapon.NeedsHalt && Math.Abs(tank.Speed) > StationarySpeed))
        {
            return false;
        }

        var laidOn = RangeAlongGun(tank)?.Vehicle;
        float spread = laidOn != null ? Spread(tank, laidOn) : ClassicRules.BaseSpread * (Math.Abs(tank.Speed) > StationarySpeed ? ClassicRules.MovingSpread : 1);
        Launch(tank, tank.Loaded, tank.TurretHeading + Gaussian() * spread * ClassicRules.AimedSpread);
        return true;
    }

    /// <summary>Traverses the gunner's turret by hand instead of letting it track targets.</summary>
    private void LayGun(Vehicle tank, float dt) =>
        tank.TurretHeading += Math.Clamp(GunnerTraverse, -1, 1) * tank.Spec.TurretTurn * dt;
}
