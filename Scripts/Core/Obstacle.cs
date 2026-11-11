namespace Breakwater.Core;

using System.Numerics;

/// <summary>An axis-aligned footprint that blocks movement, sight and fire. Units are metres.</summary>
public sealed record Obstacle(Vector2 Center, Vector2 HalfSize, float Height, ObstacleKind Kind);
