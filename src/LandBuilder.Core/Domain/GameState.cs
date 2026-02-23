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

public sealed class GameState
{
    public const int BoardWidth = 3;
    public const int BoardHeight = 3;

    public long Coins { get; set; }
    public ulong RngState { get; set; }
    public int RngStep { get; set; }
    public TileType? CurrentTile { get; set; }
    public Dictionary<int, PlacedTile> Board { get; set; } = new();
    public string LastMessage { get; set; } = "Ready";

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
        return new GameState
        {
            Coins = Coins,
            RngState = RngState,
            RngStep = RngStep,
            CurrentTile = CurrentTile,
            LastMessage = LastMessage,
            Board = Board.ToDictionary(k => k.Key, v => v.Value)
        };
    }
}
