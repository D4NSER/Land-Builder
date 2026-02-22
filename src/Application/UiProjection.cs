using LandBuilder.Domain;

namespace LandBuilder.Application;

public sealed record UiProjection(
    int Coins,
    int BuildingsCount,
    int ProductionPerTick,
    IReadOnlyDictionary<int, TileOwnership> TileOwnerships,
    string LastEventMessage)
{
    public static UiProjection From(GameState state, IEnumerable<IDomainEvent> events)
    {
        var map = state.World.Tiles.ToDictionary(x => x.Key, x => x.Value.Ownership);
        var last = events.LastOrDefault() switch
        {
            TileUnlockedEvent e => $"Tile {e.TileId} unlocked",
            BuildingPlacedEvent e => $"Placed {e.BuildingTypeId} on tile {e.TileId}",
            BuildingUpgradedEvent e => $"Building {e.BuildingId} upgraded to L{e.NewLevel}",
            CurrencySpentEvent e => $"Spent {e.Amount} coins",
            CurrencyGainedEvent e => $"+{e.Amount} coins",
            TickProcessedEvent e => $"Tick +{e.StepCount}",
            CommandRejectedEvent e => $"Action blocked: {e.Reason}",
            null => "Ready",
            _ => "Event processed"
        };

        return new UiProjection(
            Coins: state.Economy.Coins,
            BuildingsCount: state.Buildings.Count,
            ProductionPerTick: DeterministicSimulator.GetProductionPerTick(state),
            TileOwnerships: map,
            LastEventMessage: last);
    }
}
