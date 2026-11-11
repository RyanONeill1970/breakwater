namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// Enemy vehicles that arrive during a Classic battle, <see cref="At"/> seconds in, at the start of
/// <see cref="Route"/> and drive along it in order: an assault on a site the player must hold, or a
/// convoy crossing the island that the player must ambush before it leaves by the route's end.
/// </summary>
public sealed record EnemyColumn(float At, IReadOnlyList<VehicleKind> Kinds, IReadOnlyList<Vector2> Route);
