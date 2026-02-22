namespace LandBuilder.Domain;

public static class DeterministicSimulator
{
    public static (GameState State, IReadOnlyList<IDomainEvent> Events) Apply(GameState state, IGameCommand command)
    {
        return command switch
        {
            ExpandTileCommand expand => ApplyExpand(state, expand),
            TickCommand tick => ApplyTick(state, tick),
            _ => (state, new IDomainEvent[] { new CommandRejectedEvent("Unknown command") })
        };
    }

    private static (GameState, IReadOnlyList<IDomainEvent>) ApplyExpand(GameState state, ExpandTileCommand command)
    {
        if (!state.World.Tiles.TryGetValue(command.TileId, out var tile))
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Tile not found") });
        }

        if (tile.Ownership != TileOwnership.Unlockable)
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Tile is not unlockable") });
        }

        var hasUnlockedNeighbor = tile.AdjacentTileIds
            .Select(id => state.World.Tiles.TryGetValue(id, out var adjacent) ? adjacent : null)
            .Any(t => t is not null && t.Ownership == TileOwnership.Unlocked);

        if (!hasUnlockedNeighbor)
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("No adjacent unlocked tile") });
        }

        if (state.Economy.Coins < tile.UnlockCost)
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Insufficient coins") });
        }

        var mutableTiles = state.World.Tiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        mutableTiles[command.TileId] = tile with { Ownership = TileOwnership.Unlocked };

        // Promote immediate neighbors from locked to unlockable (MVP-0 simple frontier rule).
        foreach (var neighborId in tile.AdjacentTileIds)
        {
            if (!mutableTiles.TryGetValue(neighborId, out var neighbor)) continue;
            if (neighbor.Ownership == TileOwnership.Locked)
            {
                mutableTiles[neighborId] = neighbor with { Ownership = TileOwnership.Unlockable };
            }
        }

        var nextState = state with
        {
            World = new WorldState(mutableTiles),
            Economy = state.Economy with { Coins = state.Economy.Coins - tile.UnlockCost }
        };

        return (nextState, new IDomainEvent[]
        {
            new CurrencySpentEvent(tile.UnlockCost, nextState.Economy.Coins),
            new TileUnlockedEvent(command.TileId)
        });
    }

    private static (GameState, IReadOnlyList<IDomainEvent>) ApplyTick(GameState state, TickCommand command)
    {
        // MVP-0 deterministic tick placeholder: no economy growth yet, only event emission.
        var sanitizedStepCount = command.Steps < 1 ? 1 : command.Steps;
        return (state, new IDomainEvent[] { new TickProcessedEvent(sanitizedStepCount) });
    }
}
