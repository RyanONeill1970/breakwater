namespace Breakwater.Core;

public sealed class RosterSlot
{
    public int Id { get; set; }

    public float Health { get; set; } = Vehicle.MaxHealth;
    public VehicleKind Kind { get; set; }
}
