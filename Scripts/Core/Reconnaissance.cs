namespace Breakwater.Core;

using System.Numerics;

/// <summary>
/// Team observation: which enemies are currently seen, which ground has been explored, and each
/// team's memory of where it last saw the other side's vehicles.
/// </summary>
public sealed class Reconnaissance(Simulation battle)
{
    /// <summary>Exploration cells per side.</summary>
    public const int GridSize = 46;

    public const int CellCount = GridSize * GridSize;

    /// <summary>Exploration cell width in metres.</summary>
    public const float CellSize = 4;

    /// <summary>World coordinate of the first cell centre on each axis.</summary>
    public const float GridOrigin = -90;

    /// <summary>How long a remembered contact is kept after it was last seen.</summary>
    public const float MemorySeconds = 30;

    /// <summary>Observation is recomputed at this interval rather than every substep.</summary>
    private const float RefreshSeconds = .25f;

    private float untilRefresh;

    public Dictionary<int, Contact> EnemyIntel { get; } = [];
    public bool[] Explored { get; private set; } = new bool[CellCount];
    public Dictionary<int, Contact> FriendlyIntel { get; } = [];
    public bool[] Observed { get; } = new bool[CellCount];
    public int Revision { get; private set; }
    public HashSet<int> VisibleEnemies { get; } = [];

    /// <summary>Friendly vehicles the enemy currently sees: the mirror of <see cref="VisibleEnemies"/>.</summary>
    public HashSet<int> SpottedFriendlies { get; } = [];

    /// <summary>Friendly memory of enemies that are no longer in sight.</summary>
    public IEnumerable<Contact> Ghosts => FriendlyIntel.Values.Where(c =>
        !VisibleEnemies.Contains(c.Id) && !c.Destroyed && battle.Elapsed - c.LastSeen <= MemorySeconds);

    public static Vector2 CellCenter(int index) =>
        new(GridOrigin + (index % GridSize) * CellSize, GridOrigin + (index / GridSize) * CellSize);

    public bool CanObserve(Vehicle observer, Vector2 point) => CanObserve(observer, point, SightRange(observer));

    /// <summary>How far <paramref name="observer"/> sees: less far for the enemy at night.</summary>
    public float SightRange(Vehicle observer) =>
        battle.Night && observer.Enemy ? observer.Spec.SightRange * ClassicRules.NightEnemySight : observer.Spec.SightRange;

    /// <summary>
    /// Whether <paramref name="observer"/> can see <paramref name="target"/>. In Classic mode a vehicle
    /// in concealing terrain that hasn't fired lately is only seen from much closer.
    /// </summary>
    public bool CanSee(Vehicle observer, Vehicle target)
    {
        // A muzzle flash shows at night as far as by day.
        float range = battle.Night && target.Revealed > 0 ? observer.Spec.SightRange : SightRange(observer);
        return CanObserve(observer, target.Position, Concealed(target) ? range / ClassicRules.ConcealmentFactor : range);
    }

    private bool CanObserve(Vehicle observer, Vector2 point, float range) =>
        observer.Alive &&
        Vector2.DistanceSquared(observer.Position, point) <= range * range &&
        battle.Navigation.Clear(observer.Position, point) && !battle.Screened(observer.Position, point);

    public bool Concealed(Vehicle target) =>
        battle.Terrain != null && target.Revealed <= 0 && TerrainMap.Conceals(battle.Terrain.At(target.Position));

    /// <summary>Whether the other team saw <paramref name="target"/> at the last refresh.</summary>
    public bool Spotted(Vehicle target) => target.Enemy ? VisibleEnemies.Contains(target.Id) : SpottedFriendlies.Contains(target.Id);

    public bool PointVisible(Vector2 point, bool enemyTeam = false) =>
        battle.Vehicles.Any(v => v.Enemy == enemyTeam && CanObserve(v, point));

    public bool IsVisible(Vehicle unit) => !unit.Enemy || VisibleEnemies.Contains(unit.Id);

    public void Tick(float dt)
    {
        untilRefresh -= dt;
        if (untilRefresh <= 0)
        {
            Refresh();
            untilRefresh = RefreshSeconds;
        }
    }

    public void Refresh()
    {
        VisibleEnemies.Clear();
        SpottedFriendlies.Clear();
        foreach (var target in battle.Vehicles)
        {
            var intel = target.Enemy ? FriendlyIntel : EnemyIntel;
            if (!battle.Vehicles.Any(v => v.Enemy != target.Enemy && CanSee(v, target)))
            {
                continue;
            }

            (target.Enemy ? VisibleEnemies : SpottedFriendlies).Add(target.Id);

            bool discovered = !intel.ContainsKey(target.Id);
            intel[target.Id] = new Contact
            {
                Id = target.Id,
                Destroyed = !target.Alive,
                LastSeen = battle.Elapsed,
                Position = target.Position,
            };
            if (discovered && target.Enemy && target.Alive && battle.Phase == MissionPhase.Active)
            {
                battle.Events.Enqueue(new(CombatEventKind.Contact, target.Position, "Enemy contact reported"));
            }
        }

        foreach (var intel in new[] { FriendlyIntel, EnemyIntel })
        {
            var expired = intel.Keys
                .Where(id => battle.Elapsed - intel[id].LastSeen > MemorySeconds || !battle.Vehicles.Any(v => v.Id == id))
                .ToArray();
            foreach (int id in expired)
            {
                intel.Remove(id);
            }
        }

        for (int i = 0; i < Observed.Length; i++)
        {
            Observed[i] = PointVisible(CellCenter(i));
            Explored[i] |= Observed[i];
        }
        Revision++;
    }

    public ReconSnapshot Snapshot() => new()
    {
        // A column can escape between observation refreshes. Do not persist its stale contact.
        EnemyIntel = EnemyIntel.Values.Where(c => battle.Vehicles.Any(v => v.Id == c.Id)).ToArray(),
        Explored = (bool[])Explored.Clone(),
        FriendlyIntel = FriendlyIntel.Values.Where(c => battle.Vehicles.Any(v => v.Id == c.Id)).ToArray(),
    };

    public void Restore(ReconSnapshot state)
    {
        if (state.Explored == null || state.Explored.Length != Explored.Length || state.FriendlyIntel == null || state.EnemyIntel == null)
        {
            throw new FormatException("Invalid reconnaissance data.");
        }

        void RestoreSide(Contact[] contacts, Dictionary<int, Contact> destination, bool targetEnemy)
        {
            if (contacts.Length > battle.Vehicles.Count || contacts.Select(c => c?.Id).Distinct().Count() != contacts.Length)
            {
                throw new FormatException("Invalid contacts.");
            }

            foreach (var c in contacts)
            {
                if (c == null || !Navigation.Inside(c.Position) || !float.IsFinite(c.LastSeen) || c.LastSeen < 0 ||
                    c.LastSeen > battle.Elapsed + SaveGame.TimeTolerance ||
                    !battle.Vehicles.Any(v => v.Id == c.Id && v.Enemy == targetEnemy))
                {
                    throw new FormatException("Invalid contact history.");
                }

                destination[c.Id] = c;
            }
        }

        Explored = (bool[])state.Explored.Clone();
        FriendlyIntel.Clear();
        EnemyIntel.Clear();
        RestoreSide(state.FriendlyIntel, FriendlyIntel, true);
        RestoreSide(state.EnemyIntel, EnemyIntel, false);
        Refresh();
    }
}
