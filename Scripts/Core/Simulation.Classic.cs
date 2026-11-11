namespace Breakwater.Core;

using System.Numerics;

// Classic-mode combat and defenders. Vehicles pick ammunition by target, reload when they switch type,
// scatter their shots by distance, movement and the target's cover, and damage by power over armour.
// Defenders stand their ground: they fight what comes within contact range and otherwise keep to their posts.
public sealed partial class Simulation
{
    /// <summary>
    /// How far a shot from <paramref name="shooter"/> at <paramref name="target"/> may stray: the standard
    /// deviation of its bearing, in radians. The target's ground and either side moving widen it.
    /// </summary>
    public float Spread(Vehicle shooter, Vehicle target)
    {
        float spread = ClassicRules.BaseSpread;
        if (Terrain != null)
        {
            spread *= TerrainMap.Cover(Terrain.At(target.Position));
        }

        if (Math.Abs(shooter.Speed) > StationarySpeed)
        {
            spread *= ClassicRules.MovingSpread;
        }

        if (Math.Abs(target.Speed) > StationarySpeed)
        {
            spread *= ClassicRules.MovingSpread;
        }

        return spread;
    }

    /// <summary>
    /// The best ammunition <paramref name="tank"/> has for <paramref name="target"/> at this range, or null.
    /// Missiles need a halt unless <paramref name="anyMotion"/> is set (used to keep a moving launcher tracking).
    /// </summary>
    public static Weapon? ChooseWeapon(Vehicle tank, Vehicle target, bool anyMotion = false)
    {
        float distance = Vector2.Distance(tank.Position, target.Position);
        foreach (var weapon in ClassicRules.Preference(target.Kind))
        {
            var spec = WeaponSpec.For(weapon);
            if (tank.CanFire(weapon) && distance <= spec.Range && (anyMotion || !spec.NeedsHalt || Math.Abs(tank.Speed) <= StationarySpeed))
            {
                return weapon;
            }
        }

        return null;
    }

    /// <summary>The spotted enemy this vehicle can hit that poses the biggest threat: heavy armour, close by.</summary>
    private Vehicle? ClassicTarget(Vehicle tank) => Vehicles
        .Where(v => v.Alive && v.Enemy != tank.Enemy && Recon.Spotted(v) &&
            ChooseWeapon(tank, v, anyMotion: true) != null && Visible(tank, v))
        .MaxBy(v => (v.Convoy ? ConvoyTargetArmor : v.Spec.Armor) / Math.Max(1, Vector2.Distance(v.Position, tank.Position)));

    private bool TryFireClassic(Vehicle tank, Vehicle? target)
    {
        target ??= Target(tank);
        if (target == null || !target.Alive || target.Enemy == tank.Enemy || !Recon.Spotted(target) || !Visible(tank, target) ||
            Math.Abs(Geometry.Delta(tank.TurretHeading, Geometry.Angle(target.Position - tank.Position))) > FireAlignment ||
            ChooseWeapon(tank, target) is not Weapon weapon)
        {
            return false;
        }

        var spec = WeaponSpec.For(weapon);
        if (tank.Loaded != weapon)
        {
            // Changing ammunition means loading the new type first.
            tank.Loaded = weapon;
            tank.Reload = spec.Reload;
            return false;
        }

        float crew = tank.Enemy ? ClassicRules.EnemyCrewSpread : ClassicRules.PlayerCrewSpread;
        Launch(tank, weapon, tank.TurretHeading + Gaussian() * Spread(tank, target) * crew);
        return true;
    }

    /// <summary>Fires one <paramref name="weapon"/> round along <paramref name="bearing"/>: spends it, starts the reload and gives the shooter away.</summary>
    private void Launch(Vehicle tank, Weapon weapon, float bearing)
    {
        var spec = WeaponSpec.For(weapon);
        Shells.Add(new Shell
        {
            Id = nextShell++,
            Direction = Geometry.Forward(bearing),
            Owner = tank.Id,
            Position = tank.Position,
            Remaining = spec.Range,
            Weapon = weapon,
        });
        if (weapon != Weapon.MachineGun)
        {
            tank.Rounds[(int)weapon]--;
        }

        tank.Reload = spec.Reload;
        tank.Revealed = ClassicRules.RevealSeconds;
        ShotsFired++;
        Events.Enqueue(new(CombatEventKind.Shot, tank.Position, UnitId: tank.Id, PlayerVisible: !tank.Enemy || Recon.Spotted(tank)));
    }

    /// <summary>Health a Classic round takes off <paramref name="victim"/>, rolling the damage dice.</summary>
    private float ClassicDamage(Vehicle victim, Weapon weapon) => ClassicDamage(victim, WeaponSpec.For(weapon).Power);

    /// <summary>Health a hit of <paramref name="power"/> takes off <paramref name="victim"/>, rolling the damage dice.</summary>
    private float ClassicDamage(Vehicle victim, float power)
    {
        float roll = ClassicRules.MinimumDamageRoll + (float)random.NextDouble() * (1 - ClassicRules.MinimumDamageRoll);
        return ClassicRules.Damage(power, victim.Spec.Armor, roll);
    }

    /// <summary>
    /// A Classic defender stands and fights while an enemy is reported within contact range, and
    /// otherwise returns to its post. It never hunts.
    /// </summary>
    private void ThinkClassic(Vehicle tank)
    {
        tank.Think = ClassicRules.ThinkMinSeconds + (float)random.NextDouble() * (ClassicRules.ThinkMaxSeconds - ClassicRules.ThinkMinSeconds);
        if (tank.Tactic == Tactic.Assault)
        {
            // A column keeps driving its route and fights on the move.
            return;
        }

        bool contact = Recon.EnemyIntel.Values.Any(c =>
            !c.Destroyed && Elapsed - c.LastSeen <= ReportMaxAge && Vector2.Distance(c.Position, tank.Position) <= ClassicRules.ContactRange);
        if (!contact)
        {
            Head(tank, tank.Home, Tactic.Guard);
            return;
        }

        tank.Tactic = Target(tank) == null ? Tactic.Guard : Tactic.Engage;
        tank.Route.Clear();
        tank.Orders.Clear();
    }

    /// <summary>
    /// Fire discipline, for both sides in Classic battles: a vehicle waiting to be provoked opens fire
    /// when an enemy it can see comes within <see cref="ClassicRules.EngageRange"/>, or when it or a
    /// comrade near it has been hit. The player can open fire sooner with weapons free or the gun sight.
    /// The first defender of a group to open fire is reported on the radio.
    /// </summary>
    private void UpdateFireDiscipline()
    {
        if (!Classic)
        {
            return;
        }

        foreach (var tank in Vehicles.Where(v => v.Alive && v.Waiting).ToList())
        {
            bool provoked = tank.Health < Vehicle.MaxHealth ||
                Vehicles.Any(v => v.Enemy != tank.Enemy && v.Alive && Recon.Spotted(v) && Vector2.Distance(v.Position, tank.Position) <= ClassicRules.EngageRange) ||
                Vehicles.Any(v => v.Enemy == tank.Enemy && v != tank && v.Health < Vehicle.MaxHealth && Vector2.Distance(v.Position, tank.Position) <= ClassicRules.AlertRadius);
            if (!provoked)
            {
                continue;
            }

            bool first = !Vehicles.Any(v => v.Enemy && v.Alive && !v.Waiting && Vector2.Distance(v.Position, tank.Position) <= ClassicRules.AlertRadius);
            tank.Waiting = false;
            if (first && tank.Enemy)
            {
                Events.Enqueue(new(CombatEventKind.Contact, tank.Position, "Enemy garrison opening fire", PlayerVisible: Recon.Spotted(tank)));
            }
        }
    }

    /// <summary>A standard normal sample (Box–Muller).</summary>
    private float Gaussian()
    {
        double u = 1 - random.NextDouble();
        double v = random.NextDouble();
        return (float)(Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(Math.Tau * v));
    }
}
