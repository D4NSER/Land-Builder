using LandBuilder.Domain;

namespace LandBuilder.Application;

public sealed record TileUiState(
    int TileId,
    TileStateKind State,
    int ExpansionPreviewCost,
    ValidationReasonCode ExpansionReasonCode,
    string ExpansionReasonMessage,
    bool CanExpand,
    bool CanPlaceCamp,
    ValidationReasonCode CampReasonCode,
    string CampReasonMessage,
    bool CanPlaceQuarry,
    ValidationReasonCode QuarryReasonCode,
    string QuarryReasonMessage);

public sealed record UiProjection(
    int Coins,
    int BuildingsCount,
    int ProductionPerTick,
    string ActiveObjectiveId,
    string ObjectiveProgress,
    string LastCompletionMessage,
    string LastEventMessage,
    IReadOnlyList<TileUiState> TileStates)
{
    public static UiProjection From(GameState state, IEnumerable<IDomainEvent> events)
    {
        var activeObjective = state.Progression.CurrentObjectiveIndex < state.ObjectiveDefinitions.Count
            ? state.ObjectiveDefinitions[state.Progression.CurrentObjectiveIndex]
            : null;

        var last = events.LastOrDefault() switch
        {
            ObjectiveCompletedEvent e => e.Message,
            TileUnlockedEvent e => $"Tile {e.TileId} unlocked",
            BuildingPlacedEvent e => $"Placed {e.BuildingTypeId} on tile {e.TileId}",
            BuildingUpgradedEvent e => $"Building {e.BuildingId} upgraded to L{e.NewLevel}",
            CurrencySpentEvent e => $"Spent {e.Amount} coins",
            CurrencyGainedEvent e => e.Amount > 0 ? $"+{e.Amount} coins" : "No coin gain",
            TickProcessedEvent e => $"Tick +{e.StepCount}",
            CommandRejectedEvent e => $"Action blocked ({e.ReasonCode}): {e.Reason}",
            null => "Ready",
            _ => "Event processed"
        };

        var tiles = state.World.Tiles.Values
            .OrderBy(t => t.TileId)
            .Select(tile =>
            {
                var expansion = DeterministicSimulator.ValidateExpansion(state, tile.TileId);
                var camp = DeterministicSimulator.ValidatePlacement(state, "Camp", tile.TileId);
                var quarry = DeterministicSimulator.ValidatePlacement(state, "Quarry", tile.TileId);

                return new TileUiState(
                    tile.TileId,
                    expansion.TileState,
                    expansion.Cost,
                    expansion.ReasonCode,
                    expansion.Message,
                    expansion.IsValid,
                    camp.IsValid,
                    camp.ReasonCode,
                    camp.Message,
                    quarry.IsValid,
                    quarry.ReasonCode,
                    quarry.Message);
            })
            .ToList();

        return new UiProjection(
            Coins: state.Economy.Coins,
            BuildingsCount: state.Buildings.Count,
            ProductionPerTick: DeterministicSimulator.GetProductionPerTick(state),
            ActiveObjectiveId: activeObjective?.ObjectiveId ?? "All objectives complete",
            ObjectiveProgress: BuildProgressText(state, activeObjective),
            LastCompletionMessage: state.Progression.LastCompletionMessage,
            LastEventMessage: last,
            TileStates: tiles);
    }

    private static string BuildProgressText(GameState state, ObjectiveDefinition? objective)
    {
        if (objective is null) return "Complete";

        var current = objective.Type switch
        {
            ObjectiveType.UnlockTileCount => state.World.Tiles.Values.Count(t => t.Ownership == TileOwnership.Unlocked) - 1,
            ObjectiveType.PlaceBuildingTypeCount => state.Buildings.Values.Count(b => b.BuildingTypeId == objective.BuildingTypeId),
            ObjectiveType.LifetimeCoinsEarned => state.Economy.LifetimeCoinsEarned,
            ObjectiveType.UpgradeBuildingToLevelAtLeast => state.Buildings.Values
                .Where(b => b.BuildingTypeId == objective.BuildingTypeId)
                .Select(b => b.Level)
                .DefaultIfEmpty(0)
                .Max(),
            ObjectiveType.PlaceBuildingTypeOnTile => state.Buildings.Values.Any(b => b.BuildingTypeId == objective.BuildingTypeId && b.TileId == objective.TileId) ? 1 : 0,
            ObjectiveType.ProductionPerTickAtLeast => DeterministicSimulator.GetProductionPerTick(state),
            _ => 0
        };

        return $"{current}/{objective.TargetValue}";
    }
}
