namespace LandBuilder.Domain;

public static class DeterministicSimulator
{
    private static readonly Dictionary<string, BuildingDefinition> BuildingDefinitions = new()
    {
        ["Camp"] = new BuildingDefinition("Camp", baseCost: 12, baseProductionPerTick: 1, maxLevel: 3, upgradeCosts: new[] { 8, 14 })
    };

    public static (GameState State, IReadOnlyList<IDomainEvent> Events) Apply(GameState state, IGameCommand command)
    {
        return command switch
        {
            ExpandTileCommand expand => ApplyExpand(state, expand),
            PlaceBuildingCommand place => ApplyPlaceBuilding(state, place),
            UpgradeBuildingCommand upgrade => ApplyUpgradeBuilding(state, upgrade),
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

    private static (GameState, IReadOnlyList<IDomainEvent>) ApplyPlaceBuilding(GameState state, PlaceBuildingCommand command)
    {
        if (!BuildingDefinitions.TryGetValue(command.BuildingTypeId, out var definition))
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Unknown building type") });
        }

        if (!state.World.Tiles.TryGetValue(command.TileId, out var tile) || tile.Ownership != TileOwnership.Unlocked)
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Tile is not buildable") });
        }

        var buildingsOnTile = state.Buildings.Values.Count(b => b.TileId == command.TileId);
        if (buildingsOnTile >= tile.MaxBuildingSlots)
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("No building slots available") });
        }

        if (state.Economy.Coins < definition.BaseCost)
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Insufficient coins") });
        }

        var nextBuildings = state.Buildings.ToDictionary(x => x.Key, x => x.Value);
        var buildingId = state.NextBuildingId;
        nextBuildings[buildingId] = new BuildingState(buildingId, command.BuildingTypeId, command.TileId, 1);

        var nextState = state with
        {
            Buildings = nextBuildings,
            NextBuildingId = buildingId + 1,
            Economy = state.Economy with { Coins = state.Economy.Coins - definition.BaseCost }
        };

        return (nextState, new IDomainEvent[]
        {
            new CurrencySpentEvent(definition.BaseCost, nextState.Economy.Coins),
            new BuildingPlacedEvent(buildingId, command.BuildingTypeId, command.TileId)
        });
    }

    private static (GameState, IReadOnlyList<IDomainEvent>) ApplyUpgradeBuilding(GameState state, UpgradeBuildingCommand command)
    {
        if (!state.Buildings.TryGetValue(command.BuildingId, out var building))
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Building not found") });
        }

        if (!BuildingDefinitions.TryGetValue(building.BuildingTypeId, out var definition))
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Unknown building type") });
        }

        if (building.Level >= definition.MaxLevel)
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Building already max level") });
        }

        var cost = definition.UpgradeCosts[building.Level - 1];
        if (state.Economy.Coins < cost)
        {
            return (state, new IDomainEvent[] { new CommandRejectedEvent("Insufficient coins") });
        }

        var nextBuildings = state.Buildings.ToDictionary(x => x.Key, x => x.Value);
        var upgraded = building with { Level = building.Level + 1 };
        nextBuildings[building.BuildingId] = upgraded;

        var nextState = state with
        {
            Buildings = nextBuildings,
            Economy = state.Economy with { Coins = state.Economy.Coins - cost }
        };

        return (nextState, new IDomainEvent[]
        {
            new CurrencySpentEvent(cost, nextState.Economy.Coins),
            new BuildingUpgradedEvent(upgraded.BuildingId, upgraded.Level)
        });
    }

    private static (GameState, IReadOnlyList<IDomainEvent>) ApplyTick(GameState state, TickCommand command)
    {
        var sanitizedStepCount = command.Steps < 1 ? 1 : command.Steps;
        var totalProduction = 0;

        foreach (var building in state.Buildings.Values.OrderBy(x => x.BuildingId))
        {
            if (!BuildingDefinitions.TryGetValue(building.BuildingTypeId, out var definition)) continue;
            totalProduction += definition.BaseProductionPerTick * building.Level;
        }

        var gain = totalProduction * sanitizedStepCount;
        var nextState = state with
        {
            Economy = state.Economy with { Coins = state.Economy.Coins + gain }
        };

        return (nextState, new IDomainEvent[]
        {
            new TickProcessedEvent(sanitizedStepCount),
            new CurrencyGainedEvent(gain, nextState.Economy.Coins)
        });
    }

    public static int GetProductionPerTick(GameState state)
    {
        return state.Buildings.Values.Sum(b =>
        {
            if (!BuildingDefinitions.TryGetValue(b.BuildingTypeId, out var def)) return 0;
            return def.BaseProductionPerTick * b.Level;
        });
    }

    private sealed record BuildingDefinition(string BuildingTypeId, int BaseCost, int BaseProductionPerTick, int MaxLevel, int[] UpgradeCosts);
}
