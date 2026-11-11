namespace Breakwater.Core;

using System.Numerics;

// Defender AI. Defenders act only on their team's radio reports (observed positions), never on the
// live location of a hidden opponent.
public sealed partial class Simulation
{
    // Each defender rethinks at this interval, staggered by id so they don't all replan on one tick.
    private const float ThinkSeconds = 1.5f;
    private const float ThinkStagger = .15f;

    /// <summary>Reports older than this are ignored.</summary>
    private const float ReportMaxAge = 10;

    /// <summary>Defenders only respond to contacts this close to their post.</summary>
    private const float DefenseRadius = 65;

    /// <summary>A report older than this is investigated rather than engaged.</summary>
    private const float InvestigateAfter = .5f;

    // Destination handling.
    private const float ArrivedDistance = 2;
    private const float RouteReuseDistance = 5;

    // One defender in four patrols between two points either side of its post.
    private const float PatrolArrived = 3;
    private static readonly Vector2 PatrolOffset = new(6, 4);

    // Retreat when badly damaged or out of ammunition, back past the post and away from the threat.
    private const float RetreatHealth = 30;
    private const float RetreatDistance = 18;
    private const float RetreatInset = 7;

    // Engagement geometry.
    private const float HoldPositionRange = 36;
    private const float EngageStandoff = 32;
    private const float FlankStandoff = 28;
    private const float FlankOffset = 14;

    private void Think(Vehicle tank)
    {
        if (Classic)
        {
            ThinkClassic(tank);
            return;
        }

        int role = tank.Id % 4;
        tank.Think = ThinkSeconds + role * ThinkStagger;
        var objective = tank.Objective == Vehicle.NoObjective ? null : Sites[tank.Objective];
        if (objective != null && objective.Progress == 0 &&
            !Vehicles.Any(v => v.Alive && !v.Enemy && Vector2.Distance(v.Position, objective.Position) < CaptureSite.FriendlyRadius))
        {
            // Retaken: the assault force becomes the site's new garrison where it stands.
            tank.Home = tank.Position;
            tank.Objective = Vehicle.NoObjective;
            objective = null;
        }

        // An assaulting defender fights around its objective rather than its old post.
        var anchor = objective?.Position ?? tank.Home;
        var contact = Recon.EnemyIntel.Values
            .Where(c => !c.Destroyed && Elapsed - c.LastSeen <= ReportMaxAge && Vector2.Distance(c.Position, anchor) < DefenseRadius)
            .OrderBy(c => Vector2.DistanceSquared(c.Position, tank.Position))
            .FirstOrDefault();

        if (contact == null)
        {
            if (objective != null)
            {
                Head(tank, AssaultPoint(objective, role), Tactic.Assault);
            }
            else if (role == 0)
            {
                var patrol = tank.Home + new Vector2(tank.PatrolLeg == 0 ? PatrolOffset.X : -PatrolOffset.X, PatrolOffset.Y);
                if (Vector2.Distance(tank.Position, patrol) < PatrolArrived)
                {
                    tank.PatrolLeg = 1 - tank.PatrolLeg;
                }

                Head(tank, patrol, Tactic.Patrol);
            }
            else
            {
                Head(tank, tank.Home, Tactic.Guard);
            }

            return;
        }

        var away = tank.Position - contact.Position;
        away = away.LengthSquared() < .01f ? new Vector2(0, -1) : Vector2.Normalize(away);
        if (tank.Health < RetreatHealth || tank.RoundsLeft == 0)
        {
            float limit = Mission.Edge - RetreatInset;
            Head(tank, Vector2.Clamp(anchor + away * RetreatDistance, new(-limit), new(limit)), Tactic.Retreat);
            return;
        }

        if (Elapsed - contact.LastSeen > InvestigateAfter)
        {
            Head(tank, contact.Position, Tactic.Investigate);
            return;
        }

        var localTarget = Target(tank);
        if (localTarget != null && Vector2.Distance(tank.Position, localTarget.Position) < HoldPositionRange)
        {
            tank.Tactic = Tactic.Engage;
            tank.Route.Clear();
            tank.Orders.Clear();
            return;
        }

        if (role is 1 or 2)
        {
            var side = new Vector2(-away.Y, away.X) * (role == 1 ? FlankOffset : -FlankOffset);
            Head(tank, contact.Position + away * FlankStandoff + side, Tactic.Flank);
        }
        else
        {
            Head(tank, contact.Position + away * EngageStandoff, Tactic.Engage);
        }
    }

    private void Head(Vehicle tank, Vector2 point, Tactic tactic)
    {
        tank.Tactic = tactic;
        if (Vector2.Distance(tank.Position, point) < ArrivedDistance)
        {
            tank.Route.Clear();
            tank.Orders.Clear();
            tank.Speed = 0;
            return;
        }

        if (tank.Route.Count > 0 && Vector2.Distance(tank.Route[^1], point) < RouteReuseDistance)
        {
            return;
        }

        tank.Route = Navigation.FindPath(tank.Position, point);
        tank.Orders.Clear();
        if (tank.Route.Count > 0)
        {
            tank.Orders.Add(tank.Route[^1]);
        }
    }
}
