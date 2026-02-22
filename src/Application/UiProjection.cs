using LandBuilder.Domain;

namespace LandBuilder.Application;

public sealed record UiProjection(int Coins, IReadOnlyDictionary<int, TileOwnership> TileOwnerships, string LastEventMessage)
{
    public static UiProjection From(GameState state, IEnumerable<IDomainEvent> events)
    {
        var map = state.World.Tiles.ToDictionary(x => x.Key, x => x.Value.Ownership);
        var last = events.LastOrDefault() switch
        {
            TileUnlockedEvent e => $"Tile {e.TileId} unlocked",
            CurrencySpentEvent e => $"Spent {e.Amount} coins",
            TickProcessedEvent e => $"Tick +{e.StepCount}",
            CommandRejectedEvent e => $"Action blocked: {e.Reason}",
            null => "Ready",
            _ => "Event processed"
        };

        return new UiProjection(state.Economy.Coins, map, last);
    }
}
