namespace Breakwater.Core;

using System.Numerics;

// Classic escort battles: friendly supply trucks that wait, then drive their route to the end whatever
// happens. The player cannot command them, only clear the road; the enemy prefers them as targets.
public sealed partial class Simulation
{
    /// <summary>A truck within this distance of the end of the route has delivered its load.</summary>
    public const float DeliveryDistance = 14;

    /// <summary>Enemy gunners rate a truck as a threat as if it were armoured like this: above any tank.</summary>
    public const float ConvoyTargetArmor = 4000;

    public IEnumerable<Vehicle> Trucks => Vehicles.Where(v => v.Convoy);

    /// <summary>Supply trucks parked at their destination.</summary>
    public int Delivered => ZoneBattle?.Convoy is EscortConvoy convoy
        ? Trucks.Count(t => t.Alive && Vector2.Distance(t.Position, convoy.Route[^1]) < DeliveryDistance)
        : 0;

    /// <summary>Whether the convoy has set off.</summary>
    public bool ConvoyMoving => ZoneBattle?.Convoy is EscortConvoy convoy && Elapsed >= convoy.DepartsAt;

    /// <summary>Sends the trucks off at their departure time; before and after that they look after themselves.</summary>
    private void UpdateConvoy(float dt)
    {
        if (ZoneBattle?.Convoy is not EscortConvoy convoy || Elapsed < convoy.DepartsAt || Elapsed - dt >= convoy.DepartsAt)
        {
            return;
        }

        foreach (var truck in Trucks.Where(t => t.Alive))
        {
            int n = truck.Id - Mission.FirstConvoyId;
            truck.Orders.Clear();
            truck.Orders.AddRange(convoy.Route.Skip(1).Take(convoy.Route.Count - 2));
            truck.Orders.Add(Mission.ConvoyDestination(convoy, n));
            RouteToNextOrder(truck);
        }

        Events.Enqueue(new(CombatEventKind.Order, convoy.Route[0], "Convoy moving out: keep the road clear"));
    }
}
