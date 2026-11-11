namespace Breakwater.Core;

using System.Numerics;

/// <summary>A smoke screen: nothing sees through it until it clears at <see cref="Until"/> (elapsed seconds).</summary>
public sealed record SmokeCloud(Vector2 Position, float Radius, float Until);
