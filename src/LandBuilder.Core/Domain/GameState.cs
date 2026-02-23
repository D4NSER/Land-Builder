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
    Rocky = 1,
    Forest = 2,
    Clay = 3
}

public sealed record TileState(
    int TileId,
    TileOwnership Ownership,
    int UnlockCost,
    int[] AdjacentTileIds,
    int MaxBuildingSlots = 1,
    TerrainType Terrain = TerrainType.Grass,
    int RegionDepth = 0);

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
            [0] = new TileState(0, TileOwnership.Unlocked, 0, new[] { 1, 3 }, Terrain: TerrainType.Grass, RegionDepth: 0),
            [1] = new TileState(1, TileOwnership.Unlockable, 10, new[] { 0, 2, 4 }, Terrain: TerrainType.Grass, RegionDepth: 1),
            [2] = new TileState(2, TileOwnership.Locked, 18, new[] { 1, 5 }, Terrain: TerrainType.Rocky, RegionDepth: 2),
            [3] = new TileState(3, TileOwnership.Unlockable, 10, new[] { 0, 4, 6 }, Terrain: TerrainType.Forest, RegionDepth: 1),
            [4] = new TileState(4, TileOwnership.Locked, 12, new[] { 1, 3, 5, 7 }, Terrain: TerrainType.Grass, RegionDepth: 2),
            [5] = new TileState(5, TileOwnership.Locked, 16, new[] { 2, 4, 8 }, Terrain: TerrainType.Clay, RegionDepth: 3),
            [6] = new TileState(6, TileOwnership.Locked, 12, new[] { 3, 7 }, Terrain: TerrainType.Forest, RegionDepth: 2),
            [7] = new TileState(7, TileOwnership.Locked, 18, new[] { 4, 6, 8 }, Terrain: TerrainType.Grass, RegionDepth: 3),
            [8] = new TileState(8, TileOwnership.Locked, 22, new[] { 5, 7 }, Terrain: TerrainType.Rocky, RegionDepth: 4)
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
