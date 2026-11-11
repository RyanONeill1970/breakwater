namespace Breakwater.Core;

using System.Numerics;

public sealed partial class Simulation
{
    /// <summary>Radians the turret may be off target and still fire.</summary>
    private const float FireAlignment = .06f;

    // Directional armour: incoming fire within this cosine of the hull's facing is frontal/rear.
    private const float ArmorFacingThreshold = .55f;
    private const float FrontalDamage = .55f;
    private const float SideDamage = .9f;
    private const float RearDamage = 1.35f;

    /// <summary>Scouts are lightly armoured: every hit is scaled up by this.</summary>
    private const float ScoutDamagePenalty = 1.35f;

    /// <summary>Damage after directional armour. <paramref name="incoming"/> is the shell's direction of travel.</summary>
    public static float ArmorDamage(Vehicle victim, Vector2 incoming, float damage)
    {
        float armor = Facing(victim, incoming) switch
        {
            ArmorFacing.Front => FrontalDamage,
            ArmorFacing.Rear => RearDamage,
            _ => SideDamage,
        };
        if (victim.Kind == VehicleKind.Scout)
        {
            armor *= ScoutDamagePenalty;
        }

        return damage * armor;
    }

    /// <summary>The face of <paramref name="victim"/> that a shell travelling along <paramref name="incoming"/> strikes.</summary>
    public static ArmorFacing Facing(Vehicle victim, Vector2 incoming)
    {
        float facing = Vector2.Dot(Geometry.Forward(victim.Heading), -incoming);
        return facing > ArmorFacingThreshold ? ArmorFacing.Front : facing < -ArmorFacingThreshold ? ArmorFacing.Rear : ArmorFacing.Side;
    }

    /// <summary>Line of fire between two vehicles: blocked by cover and by any other hull or wreck.</summary>
    public bool Visible(Vehicle a, Vehicle b) =>
        Navigation.Clear(a.Position, b.Position) &&
        !Vehicles.Any(v => v != a && v != b && Geometry.HitCircle(a.Position, b.Position, v.Position, Vehicle.HitRadius).HasValue);

    /// <summary>
    /// The hostile this vehicle would shoot at. Modern: the nearest one it sees itself and can reach.
    /// Classic: the biggest threat its team has spotted and it has ammunition for.
    /// </summary>
    public Vehicle? Target(Vehicle tank) => tank.Spec.Classic ? ClassicTarget(tank) : Vehicles
        .Where(v => v.Alive && v.Enemy != tank.Enemy &&
            Vector2.Distance(v.Position, tank.Position) <= tank.Spec.Range &&
            Recon.CanObserve(tank, v.Position) && Visible(tank, v))
        .OrderBy(v => Vector2.DistanceSquared(v.Position, tank.Position))
        .FirstOrDefault();

    public bool TryFire(Vehicle tank, Vehicle? target = null)
    {
        if (Phase != MissionPhase.Active || Paused || !tank.Alive || tank.Reload > 0)
        {
            return false;
        }

        if (tank.Spec.Classic)
        {
            return TryFireClassic(tank, target);
        }

        if (tank.Ammo <= 0)
        {
            return false;
        }

        target ??= Target(tank);
        if (target == null || !target.Alive || target.Enemy == tank.Enemy ||
            !Recon.CanObserve(tank, target.Position) || !Visible(tank, target) ||
            Vector2.Distance(tank.Position, target.Position) > tank.Spec.Range ||
            Math.Abs(Geometry.Delta(tank.TurretHeading, Geometry.Angle(target.Position - tank.Position))) > FireAlignment)
        {
            return false;
        }

        Shells.Add(new Shell
        {
            Id = nextShell++,
            Damage = tank.Spec.Damage,
            Direction = Geometry.Forward(tank.TurretHeading),
            Owner = tank.Id,
            Position = tank.Position,
        });
        tank.Ammo--;
        tank.Reload = tank.Spec.Reload;
        ShotsFired++;
        Events.Enqueue(new(CombatEventKind.Shot, tank.Position, UnitId: tank.Id, PlayerVisible: !tank.Enemy || Recon.PointVisible(tank.Position)));
        return true;
    }

    private void AimAndFire(Vehicle tank, float dt)
    {
        if (tank.Id == GunnerId)
        {
            LayGun(tank, dt);
            return;
        }

        var target = Target(tank);
        if (target == null)
        {
            tank.TurretHeading = Geometry.Turn(tank.TurretHeading, tank.Heading, tank.Spec.TurretTurn * dt);
            return;
        }

        float angle = Geometry.Angle(target.Position - tank.Position);
        tank.TurretHeading = Geometry.Turn(tank.TurretHeading, angle, tank.Spec.TurretTurn * dt);
        if (!tank.HoldFire && !tank.Waiting)
        {
            TryFire(tank, target);
        }
    }

    // Each substep sweeps a shell's whole path against cover and hulls and applies only the nearest hit.
    private void UpdateShells(float dt)
    {
        for (int i = Shells.Count - 1; i >= 0; i--)
        {
            var shell = Shells[i];
            float travel = Shell.Speed * dt;
            var end = shell.Position + shell.Direction * travel;
            float hit = float.PositiveInfinity;
            Vehicle? victim = null;
            Obstacle? cover = null;
            foreach (var obstacle in Obstacles)
            {
                float? t = Geometry.HitBox(shell.Position, end, obstacle);
                if (t.HasValue && t.Value < hit)
                {
                    hit = t.Value;
                    victim = null;
                    cover = obstacle;
                }
            }
            foreach (var tank in Vehicles.Where(v => v.Id != shell.Owner))
            {
                float? t = Geometry.HitCircle(shell.Position, end, tank.Position, Vehicle.HitRadius);
                if (t.HasValue && t.Value < hit)
                {
                    hit = t.Value;
                    victim = tank;
                    cover = null;
                }
            }

            if (hit <= 1)
            {
                shell.Position = Vector2.Lerp(shell.Position, end, hit);
                Impact(shell, victim, cover);
                Shells.RemoveAt(i);
            }
            else
            {
                shell.Position = end;
                shell.Remaining -= travel;
                if (shell.Remaining <= 0 || !Navigation.Inside(end))
                {
                    Shells.RemoveAt(i);
                    if (shell.Weapon != null)
                    {
                        // A Classic round that fell short or flew on past everything: a miss.
                        Events.Enqueue(new(CombatEventKind.Impact, shell.Position, PlayerVisible: IsFriendly(shell.Owner), Shooter: shell.Owner));
                    }
                }
            }
        }
    }

    private void Impact(Shell shell, Vehicle? victim, Obstacle? cover)
    {
        if (cover != null && shell.Weapon is Weapon round)
        {
            DamageBuilding(cover, WeaponSpec.For(round).Power, Vehicles.Any(v => v.Id == shell.Owner && !v.Enemy));
        }

        bool wasAlive = victim is { Alive: true };
        ArmorFacing? facing = null;
        float dealt = 0;
        if (victim is { Alive: true })
        {
            facing = Facing(victim, shell.Direction);
            float damage = shell.Weapon is Weapon weapon ? ClassicDamage(victim, weapon) : ArmorDamage(victim, shell.Direction, shell.Damage);
            dealt = Math.Min(victim.Health, damage);
            victim.Health = Math.Max(0, victim.Health - damage);
            if (shell.Weapon != null && victim.Health < ClassicRules.DestroyedBelow)
            {
                victim.Health = 0;
            }
            if (!victim.Alive)
            {
                Destroyed(victim);
                victim.Route.Clear();
                victim.Orders.Clear();
                victim.Speed = 0;
            }
        }

        bool destroyed = wasAlive && victim is { Alive: false };
        bool visible = victim is { Enemy: false } || Recon.PointVisible(shell.Position);
        string message = victim is { Enemy: false }
            ? $"{Mission.UnitName(victim)} {(victim.Alive ? "taking fire" : "vehicle lost")}"
            : "";
        Events.Enqueue(new(destroyed ? CombatEventKind.Destroy : CombatEventKind.Impact, shell.Position, message, victim?.Id ?? -1,
            visible || (shell.Weapon != null && IsFriendly(shell.Owner)), facing, dealt, shell.Owner));
    }

    /// <summary>Whether vehicle <paramref name="id"/> is on the player's side.</summary>
    private bool IsFriendly(int id) => Vehicles.Any(v => v.Id == id && !v.Enemy);
}
