namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// Friendly supply trucks in a Classic escort battle. They wait at the start of <see cref="Route"/>
/// until <see cref="DepartsAt"/> seconds in, then drive it to the end whatever happens; the player
/// cannot command them, only clear the way. At least <see cref="MustArrive"/> must get there.
/// </summary>
public sealed record EscortConvoy(float DepartsAt, IReadOnlyList<Vector2> Route, int Trucks, int MustArrive);
