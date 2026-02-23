namespace LandBuilder.Domain;

public enum ObjectiveType
{
    UnlockTileCount = 0,
    PlaceBuildingTypeCount = 1,
    LifetimeCoinsEarned = 2,
    UpgradeBuildingToLevelAtLeast = 3,
    PlaceBuildingTypeOnTile = 4,
    ProductionPerTickAtLeast = 5
}

public sealed record ObjectiveDefinition(
    string ObjectiveId,
    ObjectiveType Type,
    int TargetValue,
    string? BuildingTypeId,
    int? TileId,
    int RewardCoins,
    string? RewardUnlockFlag);

public sealed record ProgressionState(
    int CurrentObjectiveIndex,
    IReadOnlyList<string> CompletedObjectiveIds,
    IReadOnlySet<string> UnlockFlags,
    string LastCompletedObjectiveId,
    string LastCompletionMessage)
{
    public static ProgressionState Initial() =>
        new(0, new List<string>(), new HashSet<string>(), string.Empty, string.Empty);
}
