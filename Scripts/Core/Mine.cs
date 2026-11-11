namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// A mine. It hurts whoever drives over it, either side. <see cref="Enemy"/> says who laid it: the
/// player knows where their own mines are, never the enemy's until one goes off.
/// </summary>
public sealed record Mine(Vector2 Position, bool Enemy, float ArmedAt);
