namespace LandBuilder.Domain;

public enum TileOwnership
{
    Locked = 0,
    Unlockable = 1,
    Unlocked = 2
}

public sealed record TileState(int TileId, TileOwnership Ownership, int UnlockCost, int[] AdjacentTileIds);

public sealed record WorldState(IReadOnlyDictionary<int, TileState> Tiles);

public sealed record EconomyState(int Coins);

public sealed record MetaState(int SchemaVersion);

public sealed record GameState(WorldState World, EconomyState Economy, MetaState Meta)
{
    public static GameState CreateMvp0Default()
    {
        var tiles = new Dictionary<int, TileState>
        {
            [0] = new TileState(0, TileOwnership.Unlocked, 0, new[] { 1 }),
            [1] = new TileState(1, TileOwnership.Unlockable, 10, new[] { 0, 2 }),
            [2] = new TileState(2, TileOwnership.Locked, 20, new[] { 1 })
        };

        return new GameState(
            new WorldState(tiles),
            new EconomyState(25),
            new MetaState(1));
    }
}
