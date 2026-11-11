namespace Breakwater.Core;

public sealed class ReconSnapshot
{
    public Contact[] EnemyIntel { get; set; } = [];
    public bool[] Explored { get; set; } = new bool[Reconnaissance.CellCount];
    public Contact[] FriendlyIntel { get; set; } = [];
}
