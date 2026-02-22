namespace LandBuilder.Domain;

public enum TileOwnership
{
    Locked = 0,
    Unlockable = 1,
    Unlocked = 2
}

public enum TerrainType
{
    Grass = 0,
    Rocky = 1
}

public sealed record TileState(
    int TileId,
    TileOwnership Ownership,
    int UnlockCost,
    int[] AdjacentTileIds,
    int MaxBuildingSlots = 1,
    TerrainType Terrain = TerrainType.Grass);

public sealed record BuildingState(int BuildingId, string BuildingTypeId, int TileId, int Level);

public sealed record WorldState(IReadOnlyDictionary<int, TileState> Tiles);

public sealed record EconomyState(int Coins, int LifetimeCoinsEarned);

public sealed record MetaState(int SchemaVersion);

public sealed record GameState(
    WorldState World,
    EconomyState Economy,
    MetaState Meta,
    IReadOnlyDictionary<int, BuildingState> Buildings,
    int NextBuildingId,
    ProgressionState Progression,
    IReadOnlyList<ObjectiveDefinition> ObjectiveDefinitions)
{
    public static GameState CreateInitial(IReadOnlyList<ObjectiveDefinition> objectiveDefinitions)
    {
        var tiles = new Dictionary<int, TileState>
        {
            [0] = new TileState(0, TileOwnership.Unlocked, 0, new[] { 1 }, Terrain: TerrainType.Grass),
            [1] = new TileState(1, TileOwnership.Unlockable, 10, new[] { 0, 2 }, Terrain: TerrainType.Grass),
            [2] = new TileState(2, TileOwnership.Locked, 20, new[] { 1 }, Terrain: TerrainType.Rocky)
        };

        return new GameState(
            new WorldState(tiles),
            new EconomyState(30, 0),
            new MetaState(3),
            new Dictionary<int, BuildingState>(),
            1,
            ProgressionState.Initial(),
            objectiveDefinitions);
    }
}
