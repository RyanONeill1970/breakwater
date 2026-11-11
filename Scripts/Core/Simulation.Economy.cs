namespace Breakwater.Core;

using System.Numerics;

// The Classic war chest and the buildings it can cost you. Buildings can be knocked down by gunfire
// and artillery; a civilian one (away from every objective) costs a fine when the player's fire
// destroys it. Every enemy vehicle destroyed pays a bounty. Both are settled when the island is won.
public sealed partial class Simulation
{
    /// <summary>
    /// Buildings resist fire as if armoured like an M1, so a stray round or two cannot flatten one:
    /// it takes several sabot hits or a well-placed barrage.
    /// </summary>
    public const float BuildingArmor = 2000;

    /// <summary>A building no nearer than this to any objective is civilian, in world metres.</summary>
    public const float CivilianDistance = 22;

    // Every obstacle the island started with, so building damage can be saved by position in the list.
    private List<Obstacle> layout = [];

    /// <summary>Health of each obstacle in the island's original layout; a building at zero is rubble.</summary>
    public float[] BuildingHealth { get; set; } = [];

    /// <summary>Bounties earned this battle, in campaign funds.</summary>
    public int Bounty { get; set; }

    /// <summary>Fines for civilian buildings the player destroyed this battle, in campaign funds.</summary>
    public int Fines { get; set; }

    public IReadOnlyList<Obstacle> Layout => layout;

    public bool Civilian(Obstacle building) =>
        building.Kind == ObstacleKind.Building && building != TargetBuilding && Sites.All(s => Vector2.Distance(s.Position, building.Center) >= CivilianDistance);

    /// <summary>The building a Destroy battle must knock down, or null.</summary>
    public Obstacle? TargetBuilding => ZoneBattle is { Target: >= 0 } battle ? layout[battle.Target] : null;

    /// <summary>The target building's health, or full health when there is none.</summary>
    public float TargetHealth => ZoneBattle is { Target: >= 0 } battle ? BuildingHealth[battle.Target] : Vehicle.MaxHealth;

    /// <summary>Knocks down every building the saved health marks as rubble.</summary>
    internal void ApplyBuildingDamage()
    {
        for (int i = 0; i < layout.Count; i++)
        {
            if (BuildingHealth[i] <= 0)
            {
                Obstacles.Remove(layout[i]);
            }
        }
    }

    private void SetUpBuildings()
    {
        layout = [.. Obstacles];
        BuildingHealth = [.. layout.Select(_ => Vehicle.MaxHealth)];
    }

    /// <summary>A hit of <paramref name="power"/> on <paramref name="building"/>, <paramref name="byPlayer"/> or not.</summary>
    private void DamageBuilding(Obstacle building, float power, bool byPlayer, float falloff = 1)
    {
        int index = layout.IndexOf(building);
        if (!Classic || building.Kind != ObstacleKind.Building || index < 0 || BuildingHealth[index] <= 0)
        {
            return;
        }

        float roll = ClassicRules.MinimumDamageRoll + (float)random.NextDouble() * (1 - ClassicRules.MinimumDamageRoll);
        BuildingHealth[index] = Math.Max(0, BuildingHealth[index] - ClassicRules.Damage(power, BuildingArmor, roll) * falloff);
        if (BuildingHealth[index] > 0)
        {
            return;
        }

        Obstacles.Remove(building);
        bool fined = byPlayer && Civilian(building);
        if (fined)
        {
            Fines += Campaign.CivilianFine;
        }

        string message = fined ? $"Civilian building destroyed: fined {Campaign.Money(Campaign.CivilianFine, GameMode.Classic)}" : "";
        Events.Enqueue(new(CombatEventKind.BuildingDestroyed, building.Center, message, PlayerVisible: true));
        Recon.Refresh();
    }

    /// <summary>Damages every building whose footprint lies within <paramref name="radius"/> of a blast.</summary>
    private void BlastBuildings(Vector2 at, float power, float radius, bool byPlayer)
    {
        foreach (var building in Obstacles.Where(o => o.Kind == ObstacleKind.Building).ToList())
        {
            var nearest = Vector2.Clamp(at, building.Center - building.HalfSize, building.Center + building.HalfSize);
            float distance = Vector2.Distance(at, nearest);
            if (distance < radius)
            {
                DamageBuilding(building, power, byPlayer, 1 - distance / radius);
            }
        }
    }

    /// <summary>Pays the bounty on an enemy vehicle that has just been destroyed.</summary>
    private void Destroyed(Vehicle victim)
    {
        if (Classic && victim.Enemy)
        {
            Bounty += Campaign.Price(victim.Kind);
        }
    }
}
