namespace Breakwater.Core;

public enum VehicleKind
{
    // Modern mode.
    MainTank,
    Scout,
    Defender,

    // Classic mode: the player's four types, then the enemy's four.
    Abrams,
    Bradley,
    M113,
    Itv,
    T72,
    T62,
    Bmp,
    Btr,

    // Classic escort battles: a friendly supply truck the player cannot command.
    Truck,
}
