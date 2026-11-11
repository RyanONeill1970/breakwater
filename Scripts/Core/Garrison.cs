namespace Breakwater.Core;

using System.Numerics;

/// <summary>One defender a Classic battle deploys: its post and its kind.</summary>
public sealed record Garrison(Vector2 Post, VehicleKind Kind);
