namespace Breakwater.Core;

using System.Numerics;

// Classic enemy columns: vehicles that arrive during a hold or ambush battle and drive a set route,
// fighting as they go. A column bound for a site assaults it and stays; a convoy that reaches the end
// of its route leaves the island and counts as escaped.
public sealed partial class Simulation
{
    /// <summary>Column vehicles come ashore this far apart along their route's first leg.</summary>
    private const float ColumnSpacing = 7;

    /// <summary>A convoy vehicle this close to the end of its route has left the island.</summary>
    private const float ExitDistance = 6;

    /// <summary>Convoy vehicles that drove off the island this battle.</summary>
    public int Escaped { get; set; }

    /// <summary>Column vehicles destroyed so far.</summary>
    public int ColumnKills => Vehicles.Count(v => v.Id >= Mission.FirstReinforcementId && !v.Alive);

    /// <summary>Whether every column has arrived and none of its vehicles is still on the move.</summary>
    public bool ColumnsSpent =>
        ZoneBattle?.Columns is { } columns && WavesLanded >= columns.Count &&
        !Vehicles.Any(v => v.Id >= Mission.FirstReinforcementId && v.Alive);

    /// <summary>For an ambush: the fraction of the convoy destroyed so far.</summary>
    public float AmbushShare => ZoneBattle is { ColumnVehicles: > 0 } battle ? (float)ColumnKills / battle.ColumnVehicles : 0;

    /// <summary>The first id of the <paramref name="column"/>th column's vehicles: ids follow on column by column.</summary>
    public int ColumnFirstId(int column) =>
        Mission.FirstReinforcementId + (ZoneBattle?.Columns?.Take(column).Sum(c => c.Kinds.Count) ?? 0);

    private void UpdateColumns()
    {
        if (ZoneBattle?.Columns is not { } columns)
        {
            return;
        }

        while (WavesLanded < columns.Count && Elapsed >= columns[WavesLanded].At)
        {
            Arrive(columns[WavesLanded], ColumnFirstId(WavesLanded));
            WavesLanded++;
        }

        if (ZoneBattle.Objective != ObjectiveKind.Ambush)
        {
            return;
        }

        foreach (var tank in Vehicles.Where(v => v.Id >= Mission.FirstReinforcementId && v.Alive && v.Orders.Count == 0 && v.Route.Count == 0).ToList())
        {
            var exit = ColumnExit(tank);
            if (exit is Vector2 end && Vector2.Distance(tank.Position, end) < ExitDistance)
            {
                Vehicles.Remove(tank);
                Escaped++;
                Events.Enqueue(new(CombatEventKind.Order, end, "A convoy vehicle has escaped"));
            }
        }
    }

    /// <summary>Puts a column ashore at the start of its route, in line behind the lead vehicle, and sends it off.</summary>
    private void Arrive(EnemyColumn column, int firstId)
    {
        var start = column.Route[0];
        var back = column.Route.Count > 1 ? Vector2.Normalize(start - column.Route[1]) : Vector2.UnitY;
        int site = ZoneBattle?.Objective == ObjectiveKind.Hold && Sites.Count > 0 ? 0 : Vehicle.NoObjective;
        for (int n = 0; n < column.Kinds.Count; n++)
        {
            if (OpenGround(start + back * (n * ColumnSpacing)) is not Vector2 spot)
            {
                continue;
            }

            var tank = new Vehicle
            {
                Id = firstId + n,
                Enemy = true,
                Group = Mission.EnemyGroup,
                Heading = Geometry.Angle(-back),
                Home = column.Route[^1],
                Kind = column.Kinds[n],
                Objective = site,
                Position = spot,
                Tactic = Tactic.Assault,
                TurretHeading = Geometry.Angle(-back),
            };
            tank.Rearm();
            tank.Orders.AddRange(column.Route.Skip(1));
            Vehicles.Add(tank);
            RouteToNextOrder(tank);
        }

        string message = ZoneBattle?.Objective == ObjectiveKind.Ambush
            ? "Intercept: enemy convoy on the move"
            : $"Intercept: enemy column heading for {Sites.FirstOrDefault()?.Name ?? "your position"}";
        Events.Enqueue(new(CombatEventKind.Reinforcement, start, message));
    }

    /// <summary>Where a column vehicle's route ends: its exit from the island, for a convoy.</summary>
    private Vector2? ColumnExit(Vehicle tank) => tank.Id >= Mission.FirstReinforcementId ? tank.Home : null;
}
