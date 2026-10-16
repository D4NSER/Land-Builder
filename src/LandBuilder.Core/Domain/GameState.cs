namespace LandBuilder.Domain;

public enum QuestKind
{
    /// <summary>Grow the region to at least the target size.</summary>
    AtLeast = 0,

    /// <summary>Hit the target size exactly; overshooting fails the quest.</summary>
    Exactly = 1
}

public sealed record Quest(
    int Id,
    Terrain Terrain,
    QuestKind Kind,
    int Target,
    SectorRef Anchor,
    int RewardTiles,
    int RewardPoints,
    int CreatedTurn);

public sealed record RunStats(
    int TilesPlaced,
    int PerfectPlacements,
    int FlawlessPlacements,
    int QuestsCompleted,
    int QuestsFailed,
    int BestStreak,
    int LargestRegion,
    Terrain LargestRegionTerrain,
    int WondersBuilt = 0)
{
    public static RunStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, Terrain.Grass);
}

public sealed class GameState
{
    public ulong Seed { get; set; }
    public WorldTheme Theme { get; set; }
    public ulong RngState { get; set; }
    public Dictionary<int, PlacedTile> Board { get; set; } = new();
    public TileSpec? CurrentTile { get; set; }
    public TileSpec? NextTile { get; set; }

    /// <summary>Tiles left after the one in hand (the "next" tile is counted here).</summary>
    public int StackCount { get; set; }

    public long Score { get; set; }
    public int Streak { get; set; }
    public List<Quest> Quests { get; set; } = new();
    public int NextQuestId { get; set; } = 1;
    public List<Terrain> Unlocked { get; set; } = new();
    public int MilestonesReached { get; set; }
    public int UndosRemaining { get; set; }
    public RunStats Stats { get; set; } = RunStats.Empty;
    public bool IsGameOver { get; set; }

    /// <summary>Wonder cards held, placeable at any time without using the stack.</summary>
    public List<WonderType> WonderCards { get; set; } = new();

    public int Turn => Stats.TilesPlaced;

    /// <summary>Total tiles still to be placed, including the one in hand.</summary>
    public int TilesLeft => (CurrentTile is null ? 0 : 1) + StackCount;

    public double StreakMultiplier => GameRules.MultiplierForStreak(Streak);

    public long NextMilestoneScore => GameRules.MilestoneThreshold(MilestonesReached);

    public long PreviousMilestoneScore => MilestonesReached == 0 ? 0 : GameRules.MilestoneThreshold(MilestonesReached - 1);

    public static GameState CreateInitial(ulong seed, WorldTheme? theme = null)
    {
        var state = new GameState
        {
            Seed = seed,
            Theme = theme ?? Themes.FromSeed(seed),
            RngState = new Rng(seed).State,
            Unlocked = Terrains.Starting.ToList(),
            StackCount = GameRules.StartingStack,
            UndosRemaining = GameRules.UndosPerRun
        };

        // Starter tile: a calm meadow with a little forest so the first placements are easy to match.
        state.Board[HexGrid.OriginSlotIndex] = new PlacedTile(
            new TileSpec(Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Forest, Terrain.Forest, Terrain.Grass), 0, 0);

        var rng = new Rng { State = state.RngState };
        state.CurrentTile = TileGenerator.Generate(ref rng, state.Unlocked, 0, theme: state.Theme);
        state.NextTile = TileGenerator.Generate(ref rng, state.Unlocked, 1, theme: state.Theme);
        state.RngState = rng.State;
        return state;
    }

    public GameState Clone() => new()
    {
        Seed = Seed,
        Theme = Theme,
        RngState = RngState,
        Board = new Dictionary<int, PlacedTile>(Board),
        CurrentTile = CurrentTile,
        NextTile = NextTile,
        StackCount = StackCount,
        Score = Score,
        Streak = Streak,
        Quests = new List<Quest>(Quests),
        NextQuestId = NextQuestId,
        Unlocked = new List<Terrain>(Unlocked),
        MilestonesReached = MilestonesReached,
        UndosRemaining = UndosRemaining,
        Stats = Stats,
        IsGameOver = IsGameOver,
        WonderCards = new List<WonderType>(WonderCards)
    };

    /// <summary>Empty slots touching the island: the only legal placement targets.</summary>
    public IReadOnlyList<int> CandidateSlots()
    {
        var result = new SortedSet<int>();
        foreach (var slot in Board.Keys)
        {
            foreach (var neighbor in HexGrid.GetNeighborSlots(slot))
            {
                if (!Board.ContainsKey(neighbor))
                    result.Add(neighbor);
            }
        }

        return result.ToArray();
    }

    /// <summary>Stable fingerprint for determinism checks.</summary>
    public string Fingerprint()
    {
        var board = string.Join(";", Board.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value.Tile}:{kv.Value.Rotation}:{kv.Value.Wonder}:{kv.Value.Built}"));
        var quests = string.Join(";", Quests.Select(q => $"{q.Id}:{q.Terrain}:{q.Kind}:{q.Target}"));
        return $"{Seed}|{Theme}|{RngState}|{Score}|{Streak}|{StackCount}|{CurrentTile}|{NextTile}|{MilestonesReached}|{string.Join(",", Unlocked)}|{quests}|{board}|{Stats}|{IsGameOver}|{UndosRemaining}|{string.Join(",", WonderCards)}";
    }
}
