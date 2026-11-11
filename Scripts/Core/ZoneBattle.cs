namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// One battle of a Classic campaign, fought for one zone of an island. Its objective, capture sites,
/// garrison and terms are its own; the island supplies the ground, cover and weather.
/// </summary>
/// <param name="Name">Shown as the briefing title.</param>
/// <param name="Briefing">What the battle is about, for the player.</param>
/// <param name="Objective">How the battle is won.</param>
/// <param name="Zone">The island zone it is fought for: 0 south-west, 1 south-east, 2 north-west, 3 north-east.</param>
/// <param name="Sites">Capture sites, for <see cref="ObjectiveKind.Capture"/>.</param>
/// <param name="Garrison">The defenders and their posts.</param>
/// <param name="Target">For <see cref="ObjectiveKind.Destroy"/>, the index of the building in the island's obstacle layout.</param>
/// <param name="Conditions">Extra terms the battle must be won on.</param>
/// <param name="Training">A training battle: no enemy guns or minefields.</param>
/// <param name="Columns">Enemy vehicles arriving during the battle (hold and ambush battles).</param>
/// <param name="HoldSeconds">For <see cref="ObjectiveKind.Hold"/>, how long into the battle the sites must be held.</param>
/// <param name="AmbushQuota">For <see cref="ObjectiveKind.Ambush"/>, the fraction of the convoy that must be destroyed.</param>
/// <param name="EnemyMines">Whether the island's enemy minefield is laid (not across the enemy's own convoy road).</param>
/// <param name="Convoy">For <see cref="ObjectiveKind.Escort"/>, the supply trucks to see through.</param>
public sealed record ZoneBattle(
    string Name,
    string Briefing,
    ObjectiveKind Objective,
    int Zone,
    IReadOnlyList<(string Name, Vector2 Position)> Sites,
    IReadOnlyList<Garrison> Garrison,
    int Target = ZoneBattle.NoTarget,
    BattleConditions? Conditions = null,
    bool Training = false,
    IReadOnlyList<EnemyColumn>? Columns = null,
    float HoldSeconds = 0,
    float AmbushQuota = 0,
    bool EnemyMines = true,
    EscortConvoy? Convoy = null)
{
    /// <summary>Enemy vehicles in all the battle's columns.</summary>
    public int ColumnVehicles => Columns?.Sum(c => c.Kinds.Count) ?? 0;

    /// <summary><see cref="Target"/> for a battle with no building to destroy.</summary>
    public const int NoTarget = -1;

    /// <summary>Zones per island, in a 2 × 2 grid.</summary>
    public const int ZoneCount = 4;
}
