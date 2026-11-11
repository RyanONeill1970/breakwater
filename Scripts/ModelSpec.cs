namespace Breakwater;

/// <summary>
/// How to fit one downloaded vehicle model (<c>assets/models</c>) into the game: which way it faces,
/// how long the real vehicle is, and where its turret is, so the turret can be cut away from the
/// hull and traverse. Turret measures are fractions of the fitted model's size.
/// </summary>
/// <param name="Path">The imported glTF scene.</param>
/// <param name="Yaw">Degrees to turn the model about the vertical so its gun points along −Z.</param>
/// <param name="Length">The real vehicle's length in metres, gun forward, before <see cref="VehicleAssets.GameScale"/>.</param>
/// <param name="TurretRing">Height of the turret ring, as a fraction of the model's height; parts above it can be turret.</param>
/// <param name="TurretCenter">Where the turret turns, fore and aft, as a fraction of the length from the middle (negative is forward).</param>
/// <param name="TurretRadius">How far from that point the turret reaches, as a fraction of the length.</param>
/// <param name="GunWidth">Half the width of the band ahead of the turret taken as the gun, as a fraction of the length.</param>
/// <param name="Repaint">A paint colour for the model: its colour if untextured, or a tint over its texture (above 1 to lighten a dark one).</param>
public sealed record ModelSpec(string Path, float Yaw, float Length, float TurretRing, float TurretCenter, float TurretRadius, float GunWidth, Color? Repaint = null);
