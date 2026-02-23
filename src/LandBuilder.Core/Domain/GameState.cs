namespace LandBuilder.Domain;

public enum EdgeType
{
    Field = 0,
    Forest = 1,
    Water = 2,
    Town = 3
}

public enum TileType
{
    Plains = 0,
    Woods = 1,
    River = 2,
    Village = 3,
    Meadow = 4,
    Lake = 5
}

public enum PlacementRejectionReason
{
    OutOfBounds = 0,
    SlotOccupied = 1,
    NoTileInHand = 2,
    IncompatibleAdjacent = 3
}

public sealed record TileDefinition(
    TileType TileType,
    EdgeType North,
    EdgeType East,
    EdgeType South,
    EdgeType West,
    int BaseCoins,
    int MatchBonusPerEdge
);

public sealed record PlacedTile(TileType TileType, int RotationQuarterTurns);

public sealed class GameState : IEquatable<GameState>
{
    private SortedSet<TileType> _unlockedTiles = new() { TileType.Plains };

    public const int BoardWidth = 3;
    public const int BoardHeight = 3;

    public long Coins { get; set; }
    public ulong RngState { get; set; }
    public int RngStep { get; set; }
    public TileType? CurrentTile { get; set; }
    public Dictionary<int, PlacedTile> Board { get; set; } = new();
    public string LastMessage { get; set; } = "Ready";
    public IReadOnlyCollection<TileType> UnlockedTiles => _unlockedTiles;

    public static IReadOnlyList<TileType> AllTileTypes { get; } = Enum.GetValues<TileType>().OrderBy(x => x).ToArray();

    public static IReadOnlyDictionary<TileType, int> UnlockCosts { get; } = new Dictionary<TileType, int>
    {
        [TileType.Plains] = 0,
        [TileType.Woods] = 10,
        [TileType.River] = 20,
        [TileType.Meadow] = 30,
        [TileType.Village] = 40,
        [TileType.Lake] = 50
    };

    public static IReadOnlyDictionary<TileType, int> ScoreBasePoints { get; } = new Dictionary<TileType, int>
    {
        [TileType.Plains] = 1,
        [TileType.Woods] = 2,
        [TileType.River] = 3,
        [TileType.Meadow] = 4,
        [TileType.Village] = 5,
        [TileType.Lake] = 6
    };

    public int Score => ComputeScore(Board);

    public static GameState CreateInitial(ulong seed = 0xC0FFEEUL)
    {
        return new GameState
        {
            Coins = 0,
            RngState = seed,
            RngStep = 0,
            CurrentTile = null,
            Board = new Dictionary<int, PlacedTile>(),
            LastMessage = "Draw a tile to begin."
        };
    }

    public GameState Clone()
    {
        var clone = new GameState
        {
            Coins = Coins,
            RngState = RngState,
            RngStep = RngStep,
            CurrentTile = CurrentTile,
            LastMessage = LastMessage,
            Board = Board.ToDictionary(k => k.Key, v => v.Value)
        };
        clone.SetUnlockedTiles(_unlockedTiles);
        return clone;
    }

    public bool IsUnlocked(TileType tile) => _unlockedTiles.Contains(tile);

    public GameState Unlock(TileType tile)
    {
        if (_unlockedTiles.Contains(tile))
            return this;

        var cost = UnlockCosts.TryGetValue(tile, out var value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(tile), tile, "Unknown tile unlock cost.");

        if (Coins < cost)
            throw new InvalidOperationException("Not enough coins to unlock tile.");

        var next = Clone();
        next.Coins -= cost;
        next._unlockedTiles.Add(tile);
        next.LastMessage = $"Unlocked {tile} for {cost} coins.";
        return next;
    }

    public void SetUnlockedTiles(IEnumerable<TileType> tiles)
    {
        var normalized = new SortedSet<TileType>(tiles);
        if (normalized.Count == 0)
            normalized.Add(TileType.Plains);

        _unlockedTiles = normalized;
    }

    internal void ApplySnapshotFrom(GameState snapshot)
    {
        Coins = snapshot.Coins;
        RngState = snapshot.RngState;
        RngStep = snapshot.RngStep;
        CurrentTile = snapshot.CurrentTile;
        Board = snapshot.Board.ToDictionary(k => k.Key, v => v.Value);
        LastMessage = snapshot.LastMessage;
        SetUnlockedTiles(snapshot.UnlockedTiles);
    }

    public bool Equals(GameState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Coins != other.Coins ||
            RngState != other.RngState ||
            RngStep != other.RngStep ||
            CurrentTile != other.CurrentTile ||
            Score != other.Score ||
            LastMessage != other.LastMessage ||
            Board.Count != other.Board.Count ||
            _unlockedTiles.Count != other._unlockedTiles.Count)
        {
            return false;
        }

        if (!_unlockedTiles.SetEquals(other._unlockedTiles))
            return false;

        foreach (var kv in Board)
        {
            if (!other.Board.TryGetValue(kv.Key, out var value) || value != kv.Value)
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is GameState other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Coins);
        hash.Add(RngState);
        hash.Add(RngStep);
        hash.Add(CurrentTile);
        hash.Add(Score);
        hash.Add(LastMessage);
        foreach (var tile in _unlockedTiles)
            hash.Add(tile);
        foreach (var kv in Board.OrderBy(x => x.Key))
        {
            hash.Add(kv.Key);
            hash.Add(kv.Value);
        }

        return hash.ToHashCode();
    }

    private static int ComputeScore(IReadOnlyDictionary<int, PlacedTile> board)
    {
        var score = 0;

        foreach (var placed in board.Values)
        {
            if (!ScoreBasePoints.TryGetValue(placed.TileType, out var points))
                throw new InvalidOperationException($"Missing score rule for tile type {placed.TileType}.");

            score += points;
        }

        foreach (var kv in board.OrderBy(x => x.Key))
        {
            var slot = kv.Key;
            var tileType = kv.Value.TileType;
            var x = slot % BoardWidth;
            var y = slot / BoardWidth;

            if (x < BoardWidth - 1 &&
                board.TryGetValue(slot + 1, out var right) &&
                right.TileType == tileType)
            {
                score += 2;
            }

            if (y < BoardHeight - 1 &&
                board.TryGetValue(slot + BoardWidth, out var down) &&
                down.TileType == tileType)
            {
                score += 2;
            }
        }

        if (board.Count == BoardWidth * BoardHeight)
            score += 10;

        return score;
    }
}
