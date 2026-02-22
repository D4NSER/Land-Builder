namespace LandBuilder.Domain;

public static class DeterministicSimulator
{
    private static readonly Dictionary<string, BuildingDefinition> BuildingDefinitions = new()
    {
        ["Camp"] = new BuildingDefinition("Camp", 12, 1, 3, new[] { 8, 14 }, RequiredUnlockFlag: null, RequiredTerrain: TerrainType.Grass),
        ["Quarry"] = new BuildingDefinition("Quarry", 18, 2, 3, new[] { 12, 20 }, RequiredUnlockFlag: "UNLOCK_QUARRY", RequiredTerrain: TerrainType.Rocky)
    };

    public static (GameState State, IReadOnlyList<IDomainEvent> Events) Apply(GameState state, IGameCommand command)
    {
        var (nextState, events) = command switch
        {
            ExpandTileCommand expand => ApplyExpand(state, expand),
            PlaceBuildingCommand place => ApplyPlaceBuilding(state, place),
            UpgradeBuildingCommand upgrade => ApplyUpgradeBuilding(state, upgrade),
            TickCommand tick => ApplyTick(state, tick),
            _ => (state, new List<IDomainEvent> { new CommandRejectedEvent("Unknown command") })
        };

        return EvaluateObjectives(nextState, events);
    }

    private static (GameState, List<IDomainEvent>) ApplyExpand(GameState state, ExpandTileCommand command)
    {
        if (!state.World.Tiles.TryGetValue(command.TileId, out var tile))
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Tile not found") });

        if (tile.Ownership != TileOwnership.Unlockable)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Tile is not unlockable") });

        var hasUnlockedNeighbor = tile.AdjacentTileIds
            .Select(id => state.World.Tiles.TryGetValue(id, out var adjacent) ? adjacent : null)
            .Any(t => t is not null && t.Ownership == TileOwnership.Unlocked);

        if (!hasUnlockedNeighbor)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("No adjacent unlocked tile") });

        if (state.Economy.Coins < tile.UnlockCost)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Insufficient coins") });

        var mutableTiles = state.World.Tiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        mutableTiles[command.TileId] = tile with { Ownership = TileOwnership.Unlocked };

        foreach (var neighborId in tile.AdjacentTileIds)
        {
            if (!mutableTiles.TryGetValue(neighborId, out var neighbor)) continue;
            if (neighbor.Ownership == TileOwnership.Locked)
                mutableTiles[neighborId] = neighbor with { Ownership = TileOwnership.Unlockable };
        }

        var nextState = state with
        {
            World = new WorldState(mutableTiles),
            Economy = state.Economy with { Coins = state.Economy.Coins - tile.UnlockCost }
        };

        return (nextState, new List<IDomainEvent>
        {
            new CurrencySpentEvent(tile.UnlockCost, nextState.Economy.Coins),
            new TileUnlockedEvent(command.TileId)
        });
    }

    private static (GameState, List<IDomainEvent>) ApplyPlaceBuilding(GameState state, PlaceBuildingCommand command)
    {
        if (!BuildingDefinitions.TryGetValue(command.BuildingTypeId, out var definition))
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Unknown building type") });

        if (definition.RequiredUnlockFlag is not null && !state.Progression.UnlockFlags.Contains(definition.RequiredUnlockFlag))
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Building type not unlocked") });

        if (!state.World.Tiles.TryGetValue(command.TileId, out var tile) || tile.Ownership != TileOwnership.Unlocked)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Tile is not buildable") });

        if (tile.Terrain != definition.RequiredTerrain)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Terrain mismatch for building") });

        var buildingsOnTile = state.Buildings.Values.Count(b => b.TileId == command.TileId);
        if (buildingsOnTile >= tile.MaxBuildingSlots)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("No building slots available") });

        if (state.Economy.Coins < definition.BaseCost)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Insufficient coins") });

        var nextBuildings = state.Buildings.ToDictionary(x => x.Key, x => x.Value);
        var buildingId = state.NextBuildingId;
        nextBuildings[buildingId] = new BuildingState(buildingId, command.BuildingTypeId, command.TileId, 1);

        var nextState = state with
        {
            Buildings = nextBuildings,
            NextBuildingId = buildingId + 1,
            Economy = state.Economy with { Coins = state.Economy.Coins - definition.BaseCost }
        };

        return (nextState, new List<IDomainEvent>
        {
            new CurrencySpentEvent(definition.BaseCost, nextState.Economy.Coins),
            new BuildingPlacedEvent(buildingId, command.BuildingTypeId, command.TileId)
        });
    }

    private static (GameState, List<IDomainEvent>) ApplyUpgradeBuilding(GameState state, UpgradeBuildingCommand command)
    {
        if (!state.Buildings.TryGetValue(command.BuildingId, out var building))
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Building not found") });

        if (!BuildingDefinitions.TryGetValue(building.BuildingTypeId, out var definition))
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Unknown building type") });

        if (building.Level >= definition.MaxLevel)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Building already max level") });

        var cost = definition.UpgradeCosts[building.Level - 1];
        if (state.Economy.Coins < cost)
            return (state, new List<IDomainEvent> { new CommandRejectedEvent("Insufficient coins") });

        var nextBuildings = state.Buildings.ToDictionary(x => x.Key, x => x.Value);
        var upgraded = building with { Level = building.Level + 1 };
        nextBuildings[building.BuildingId] = upgraded;

        var nextState = state with
        {
            Buildings = nextBuildings,
            Economy = state.Economy with { Coins = state.Economy.Coins - cost }
        };

        return (nextState, new List<IDomainEvent>
        {
            new CurrencySpentEvent(cost, nextState.Economy.Coins),
            new BuildingUpgradedEvent(upgraded.BuildingId, upgraded.Level)
        });
    }

    private static (GameState, List<IDomainEvent>) ApplyTick(GameState state, TickCommand command)
    {
        var sanitizedStepCount = command.Steps < 1 ? 1 : command.Steps;
        var gain = GetProductionPerTick(state) * sanitizedStepCount;

        var nextState = state with
        {
            Economy = state.Economy with
            {
                Coins = state.Economy.Coins + gain,
                LifetimeCoinsEarned = state.Economy.LifetimeCoinsEarned + gain
            }
        };

        return (nextState, new List<IDomainEvent>
        {
            new TickProcessedEvent(sanitizedStepCount),
            new CurrencyGainedEvent(gain, nextState.Economy.Coins)
        });
    }

    private static (GameState, IReadOnlyList<IDomainEvent>) EvaluateObjectives(GameState state, List<IDomainEvent> events)
    {
        var nextState = state;
        var mutableEvents = new List<IDomainEvent>(events);

        while (nextState.Progression.CurrentObjectiveIndex < nextState.ObjectiveDefinitions.Count)
        {
            var objective = nextState.ObjectiveDefinitions[nextState.Progression.CurrentObjectiveIndex];
            if (!IsObjectiveMet(nextState, objective)) break;

            var unlockFlags = nextState.Progression.UnlockFlags.ToHashSet();
            if (!string.IsNullOrWhiteSpace(objective.RewardUnlockFlag))
            {
                unlockFlags.Add(objective.RewardUnlockFlag!);
                mutableEvents.Add(new UnlockFlagGrantedEvent(objective.RewardUnlockFlag!));
            }

            var completed = nextState.Progression.CompletedObjectiveIds.ToList();
            completed.Add(objective.ObjectiveId);
            var message = $"Objective complete: {objective.ObjectiveId}";

            var progression = nextState.Progression with
            {
                CurrentObjectiveIndex = nextState.Progression.CurrentObjectiveIndex + 1,
                CompletedObjectiveIds = completed,
                UnlockFlags = unlockFlags,
                LastCompletedObjectiveId = objective.ObjectiveId,
                LastCompletionMessage = message
            };

            nextState = nextState with
            {
                Progression = progression,
                Economy = nextState.Economy with
                {
                    Coins = nextState.Economy.Coins + objective.RewardCoins,
                    LifetimeCoinsEarned = nextState.Economy.LifetimeCoinsEarned + objective.RewardCoins
                }
            };

            if (objective.RewardCoins > 0)
                mutableEvents.Add(new CurrencyGainedEvent(objective.RewardCoins, nextState.Economy.Coins));

            mutableEvents.Add(new ObjectiveCompletedEvent(objective.ObjectiveId, message));
        }

        return (nextState, mutableEvents);
    }

    private static bool IsObjectiveMet(GameState state, ObjectiveDefinition objective)
    {
        return objective.Type switch
        {
            ObjectiveType.UnlockTileCount => state.World.Tiles.Values.Count(t => t.Ownership == TileOwnership.Unlocked) - 1 >= objective.TargetValue,
            ObjectiveType.PlaceBuildingTypeCount => state.Buildings.Values.Count(b => b.BuildingTypeId == objective.BuildingTypeId) >= objective.TargetValue,
            ObjectiveType.LifetimeCoinsEarned => state.Economy.LifetimeCoinsEarned >= objective.TargetValue,
            ObjectiveType.UpgradeBuildingToLevelAtLeast => state.Buildings.Values.Any(b => b.BuildingTypeId == objective.BuildingTypeId && b.Level >= objective.TargetValue),
            ObjectiveType.PlaceBuildingTypeOnTile => state.Buildings.Values.Any(b => b.BuildingTypeId == objective.BuildingTypeId && b.TileId == objective.TileId),
            ObjectiveType.ProductionPerTickAtLeast => GetProductionPerTick(state) >= objective.TargetValue,
            _ => false
        };
    }

    public static int GetProductionPerTick(GameState state)
    {
        return state.Buildings.Values
            .OrderBy(b => b.BuildingId)
            .Sum(b => BuildingDefinitions.TryGetValue(b.BuildingTypeId, out var def) ? def.BaseProductionPerTick * b.Level : 0);
    }

    private sealed record BuildingDefinition(
        string BuildingTypeId,
        int BaseCost,
        int BaseProductionPerTick,
        int MaxLevel,
        int[] UpgradeCosts,
        string? RequiredUnlockFlag,
        TerrainType RequiredTerrain);
}
