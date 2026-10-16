namespace LandBuilder.Domain;

public enum EdgeType
{
    Plains = 0,
    Forest = 1,
    Beach = 2,
    City = 3,
    Farmland = 4,
    Mountain = 5,
    Desert = 6,
    Snow = 7,
    Water = 8,
    Swamp = 9,
    Savanna = 10,
    Tundra = 11,
    Coral = 12,
    Mushroom = 13
}

public enum TileType
{
    Plains = 0,
    Woods = 1,
    River = 2,
    Village = 3,
    Meadow = 4,
    Lake = 5,
    Desert = 6,
    Snow = 7,
    Volcano = 8,
    Pond = 9,
    Ruins = 10,
    Swamp = 11,
    Savanna = 12,
    Tundra = 13,
    Coral = 14,
    Mushroom = 15
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
    EdgeType EdgeType,
    int BaseCoins,
    int MatchBonusPerEdge
);

public readonly record struct SectorPattern(
    TileType S0,
    TileType S1,
    TileType S2,
    TileType S3,
    TileType S4,
    TileType S5)
{
    public TileType this[int index] => index switch
    {
        0 => S0,
        1 => S1,
        2 => S2,
        3 => S3,
        4 => S4,
        5 => S5,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Sector index must be 0..5.")
    };

    public TileType[] ToArray() => [S0, S1, S2, S3, S4, S5];

    public TileType GetEdgeTerrain(int edgeIndex, int rotationSteps)
    {
        var normalized = ((edgeIndex + rotationSteps) % 6 + 6) % 6;
        return this[normalized];
    }

    public TileType GetDominantTerrain()
    {
        return ToArray()
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First()
            .Key;
    }

    public static SectorPattern Pure(TileType tileType) => new(tileType, tileType, tileType, tileType, tileType, tileType);
}

public sealed record PlacedTile(TileType TileType, int RotationQuarterTurns, SectorPattern? SectorPattern = null)
{
    public SectorPattern ResolvedSectorPattern => SectorPattern ?? Domain.SectorPattern.Pure(TileType);
}

public sealed class GameState : IEquatable<GameState>
{
    private SortedSet<TileType> _unlockedTiles = new() { TileType.Plains };

    public const int BoardWidth = 3;
    public const int BoardHeight = 3;

    public long Coins { get; set; }
    public ulong RngState { get; set; }
    public int RngStep { get; set; }
    public TileSectorGenerator.ThemeContext ThemeContext { get; set; }
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
        [TileType.Lake] = 50,
        [TileType.Desert] = 60,
        [TileType.Snow] = 70,
        [TileType.Volcano] = 80,
        [TileType.Pond] = 90,
        [TileType.Ruins] = 100,
        [TileType.Swamp] = 110,
        [TileType.Savanna] = 120,
        [TileType.Tundra] = 130,
        [TileType.Coral] = 140,
        [TileType.Mushroom] = 150
    };

    public int Score => ComputeScore(Board);

    public static GameState CreateInitial(ulong seed = 0xC0FFEEUL)
    {
        return new GameState
        {
            Coins = 0,
            RngState = seed,
            RngStep = 0,
            ThemeContext = TileSectorGenerator.ThemeContext.None,
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
            ThemeContext = ThemeContext,
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
        ThemeContext = snapshot.ThemeContext;
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
            ThemeContext != other.ThemeContext ||
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
        hash.Add(ThemeContext);
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

        foreach (var kv in board)
        {
            var source = kv.Value;
            foreach (var neighborSlot in HexGrid.GetNeighborSlots(kv.Key))
            {
                if (!board.TryGetValue(neighborSlot, out var neighbor))
                    continue;

                if (neighborSlot < kv.Key)
                    continue;

                var sourceEdge = GetEdgeIndexFacingNeighbor(kv.Key, neighborSlot);
                var neighborEdge = GetEdgeIndexFacingNeighbor(neighborSlot, kv.Key);
                if (sourceEdge < 0 || neighborEdge < 0)
                    continue;

                var sourceTerrain = source.ResolvedSectorPattern.GetEdgeTerrain(sourceEdge, source.RotationQuarterTurns);
                var neighborTerrain = neighbor.ResolvedSectorPattern.GetEdgeTerrain(neighborEdge, neighbor.RotationQuarterTurns);
                if (AreTerrainTypesCompatible(sourceTerrain, neighborTerrain))
                    score += 1;
            }
        }

        return score;
    }

    public static int GetEdgeIndexFacingNeighbor(int slotIndex, int neighborSlot)
    {
        var source = HexGrid.Decode(slotIndex);
        var target = HexGrid.Decode(neighborSlot);
        var dq = target.Q - source.Q;
        var dr = target.R - source.R;
        return (dq, dr) switch
        {
            (1, 0) => 0,
            (1, -1) => 1,
            (0, -1) => 2,
            (-1, 0) => 3,
            (-1, 1) => 4,
            (0, 1) => 5,
            _ => -1
        };
    }

    public static EdgeType GetEdgeType(TileType tileType) => tileType switch
    {
        TileType.Plains => EdgeType.Plains,
        TileType.Woods => EdgeType.Forest,
        TileType.River => EdgeType.Beach,
        TileType.Village => EdgeType.City,
        TileType.Meadow => EdgeType.Farmland,
        TileType.Lake => EdgeType.Mountain,
        TileType.Desert => EdgeType.Desert,
        TileType.Snow => EdgeType.Snow,
        TileType.Volcano => EdgeType.Mountain,
        TileType.Pond => EdgeType.Water,
        TileType.Ruins => EdgeType.City,
        TileType.Swamp => EdgeType.Swamp,
        TileType.Savanna => EdgeType.Savanna,
        TileType.Tundra => EdgeType.Tundra,
        TileType.Coral => EdgeType.Coral,
        TileType.Mushroom => EdgeType.Mushroom,
        _ => throw new ArgumentOutOfRangeException(nameof(tileType), tileType, "Unknown tile edge type.")
    };

    public static bool AreTerrainTypesCompatible(TileType a, TileType b)
    {
        if (GetEdgeType(a) == GetEdgeType(b))
            return true;

        return (a, b) switch
        {
            (TileType.Plains, TileType.Meadow) or (TileType.Meadow, TileType.Plains) => true,
            (TileType.Plains, TileType.Savanna) or (TileType.Savanna, TileType.Plains) => true,
            (TileType.Woods, TileType.Swamp) or (TileType.Swamp, TileType.Woods) => true,
            (TileType.Woods, TileType.Mushroom) or (TileType.Mushroom, TileType.Woods) => true,
            (TileType.Lake, TileType.Volcano) or (TileType.Volcano, TileType.Lake) => true,
            (TileType.Lake, TileType.Tundra) or (TileType.Tundra, TileType.Lake) => true,
            (TileType.River, TileType.Coral) or (TileType.Coral, TileType.River) => true,
            (TileType.River, TileType.Pond) or (TileType.Pond, TileType.River) => true,
            (TileType.Snow, TileType.Tundra) or (TileType.Tundra, TileType.Snow) => true,
            (TileType.Village, TileType.Ruins) or (TileType.Ruins, TileType.Village) => true,
            (TileType.Desert, TileType.Savanna) or (TileType.Savanna, TileType.Desert) => true,
            _ => false
        };
    }
}
