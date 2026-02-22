namespace LandBuilder.Domain;

public enum ValidationReasonCode
{
    None = 0,
    UnknownCommand = 1,
    TileNotFound = 2,
    TileNotUnlockable = 3,
    NoAdjacentUnlockedTile = 4,
    InsufficientCoins = 5,
    UnknownBuildingType = 6,
    BuildingTypeNotUnlocked = 7,
    TileNotBuildable = 8,
    TerrainMismatch = 9,
    NoBuildingSlotsAvailable = 10,
    BuildingNotFound = 11,
    BuildingAlreadyMaxLevel = 12
}

public enum TileStateKind
{
    Locked = 0,
    Unlockable = 1,
    Unlocked = 2,
    Buildable = 3
}

public sealed record ExpansionValidationResult(
    bool IsValid,
    ValidationReasonCode ReasonCode,
    string Message,
    int Cost,
    int TileDepth,
    int NextUnlockIndex,
    TileStateKind TileState);

public sealed record PlacementValidationResult(
    bool IsValid,
    ValidationReasonCode ReasonCode,
    string Message,
    int Cost,
    TileStateKind TileState);

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
            _ => (state, new List<IDomainEvent> { Reject(ValidationReasonCode.UnknownCommand) })
        };

        return EvaluateObjectives(nextState, events);
    }

    public static ExpansionValidationResult ValidateExpansion(GameState state, int tileId)
    {
        if (!state.World.Tiles.TryGetValue(tileId, out var tile))
            return InvalidExpansion(ValidationReasonCode.TileNotFound, 0, 0, 0, TileStateKind.Locked);

        var nextUnlockIndex = state.World.Tiles.Values.Count(t => t.Ownership == TileOwnership.Unlocked);
        var tileDepth = ComputeTileDepth(state.World, tileId);
        var cost = ComputeExpansionCost(tile.UnlockCost, tileDepth, nextUnlockIndex);

        if (tile.Ownership != TileOwnership.Unlockable)
            return InvalidExpansion(ValidationReasonCode.TileNotUnlockable, cost, tileDepth, nextUnlockIndex, ToTileStateKind(state, tile));

        var hasUnlockedNeighbor = tile.AdjacentTileIds
            .Select(id => state.World.Tiles.TryGetValue(id, out var adjacent) ? adjacent : null)
            .Any(t => t is not null && t.Ownership == TileOwnership.Unlocked);

        if (!hasUnlockedNeighbor)
            return InvalidExpansion(ValidationReasonCode.NoAdjacentUnlockedTile, cost, tileDepth, nextUnlockIndex, ToTileStateKind(state, tile));

        if (state.Economy.Coins < cost)
            return InvalidExpansion(ValidationReasonCode.InsufficientCoins, cost, tileDepth, nextUnlockIndex, ToTileStateKind(state, tile));

        return new ExpansionValidationResult(true, ValidationReasonCode.None, ValidationMessage(ValidationReasonCode.None), cost, tileDepth, nextUnlockIndex, TileStateKind.Unlockable);
    }

    public static PlacementValidationResult ValidatePlacement(GameState state, string buildingTypeId, int tileId)
    {
        if (!BuildingDefinitions.TryGetValue(buildingTypeId, out var definition))
            return InvalidPlacement(ValidationReasonCode.UnknownBuildingType, 0, TileStateKind.Locked);

        if (definition.RequiredUnlockFlag is not null && !state.Progression.UnlockFlags.Contains(definition.RequiredUnlockFlag))
            return InvalidPlacement(ValidationReasonCode.BuildingTypeNotUnlocked, definition.BaseCost, TileStateKind.Locked);

        if (!state.World.Tiles.TryGetValue(tileId, out var tile))
            return InvalidPlacement(ValidationReasonCode.TileNotFound, definition.BaseCost, TileStateKind.Locked);

        var tileState = ToTileStateKind(state, tile, definition);
        if (tile.Ownership != TileOwnership.Unlocked)
            return InvalidPlacement(ValidationReasonCode.TileNotBuildable, definition.BaseCost, tileState);

        if (tile.Terrain != definition.RequiredTerrain)
            return InvalidPlacement(ValidationReasonCode.TerrainMismatch, definition.BaseCost, tileState);

        var buildingsOnTile = state.Buildings.Values.Count(b => b.TileId == tileId);
        if (buildingsOnTile >= tile.MaxBuildingSlots)
            return InvalidPlacement(ValidationReasonCode.NoBuildingSlotsAvailable, definition.BaseCost, tileState);

        if (state.Economy.Coins < definition.BaseCost)
            return InvalidPlacement(ValidationReasonCode.InsufficientCoins, definition.BaseCost, tileState);

        return new PlacementValidationResult(true, ValidationReasonCode.None, ValidationMessage(ValidationReasonCode.None), definition.BaseCost, TileStateKind.Buildable);
    }

    public static string ValidationMessage(ValidationReasonCode code)
    {
        return code switch
        {
            ValidationReasonCode.None => "Valid",
            ValidationReasonCode.UnknownCommand => "Unknown command",
            ValidationReasonCode.TileNotFound => "Tile not found",
            ValidationReasonCode.TileNotUnlockable => "Tile is not unlockable",
            ValidationReasonCode.NoAdjacentUnlockedTile => "No adjacent unlocked tile",
            ValidationReasonCode.InsufficientCoins => "Insufficient coins",
            ValidationReasonCode.UnknownBuildingType => "Unknown building type",
            ValidationReasonCode.BuildingTypeNotUnlocked => "Building type not unlocked",
            ValidationReasonCode.TileNotBuildable => "Tile is not buildable",
            ValidationReasonCode.TerrainMismatch => "Terrain mismatch for building",
            ValidationReasonCode.NoBuildingSlotsAvailable => "No building slots available",
            ValidationReasonCode.BuildingNotFound => "Building not found",
            ValidationReasonCode.BuildingAlreadyMaxLevel => "Building already max level",
            _ => "Unknown validation error"
        };
    }

    private static (GameState, List<IDomainEvent>) ApplyExpand(GameState state, ExpandTileCommand command)
    {
        var validation = ValidateExpansion(state, command.TileId);
        if (!validation.IsValid)
            return (state, new List<IDomainEvent> { Reject(validation.ReasonCode) });

        var mutableTiles = state.World.Tiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        var tile = mutableTiles[command.TileId];
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
            Economy = state.Economy with { Coins = state.Economy.Coins - validation.Cost }
        };

        return (nextState, new List<IDomainEvent>
        {
            new CurrencySpentEvent(validation.Cost, nextState.Economy.Coins),
            new TileUnlockedEvent(command.TileId)
        });
    }

    private static (GameState, List<IDomainEvent>) ApplyPlaceBuilding(GameState state, PlaceBuildingCommand command)
    {
        var validation = ValidatePlacement(state, command.BuildingTypeId, command.TileId);
        if (!validation.IsValid)
            return (state, new List<IDomainEvent> { Reject(validation.ReasonCode) });

        var nextBuildings = state.Buildings.ToDictionary(x => x.Key, x => x.Value);
        var buildingId = state.NextBuildingId;
        nextBuildings[buildingId] = new BuildingState(buildingId, command.BuildingTypeId, command.TileId, 1);

        var nextState = state with
        {
            Buildings = nextBuildings,
            NextBuildingId = buildingId + 1,
            Economy = state.Economy with { Coins = state.Economy.Coins - validation.Cost }
        };

        return (nextState, new List<IDomainEvent>
        {
            new CurrencySpentEvent(validation.Cost, nextState.Economy.Coins),
            new BuildingPlacedEvent(buildingId, command.BuildingTypeId, command.TileId)
        });
    }

    private static (GameState, List<IDomainEvent>) ApplyUpgradeBuilding(GameState state, UpgradeBuildingCommand command)
    {
        if (!state.Buildings.TryGetValue(command.BuildingId, out var building))
            return (state, new List<IDomainEvent> { Reject(ValidationReasonCode.BuildingNotFound) });

        if (!BuildingDefinitions.TryGetValue(building.BuildingTypeId, out var definition))
            return (state, new List<IDomainEvent> { Reject(ValidationReasonCode.UnknownBuildingType) });

        if (building.Level >= definition.MaxLevel)
            return (state, new List<IDomainEvent> { Reject(ValidationReasonCode.BuildingAlreadyMaxLevel) });

        var cost = definition.UpgradeCosts[building.Level - 1];
        if (state.Economy.Coins < cost)
            return (state, new List<IDomainEvent> { Reject(ValidationReasonCode.InsufficientCoins) });

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

    private static int ComputeExpansionCost(int baseUnlockCost, int tileDepth, int nextUnlockIndex)
    {
        var indexPremium = Math.Max(0, nextUnlockIndex - 2) * 4;
        var depthPremium = Math.Max(0, tileDepth - 2) * 3;
        return baseUnlockCost + indexPremium + depthPremium;
    }

    private static int ComputeTileDepth(WorldState world, int tileId)
    {
        if (tileId == 0) return 0;

        var visited = new HashSet<int> { 0 };
        var queue = new Queue<(int TileId, int Depth)>();
        queue.Enqueue((0, 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!world.Tiles.TryGetValue(current.TileId, out var tile))
                continue;

            foreach (var adjacent in tile.AdjacentTileIds.OrderBy(x => x))
            {
                if (!visited.Add(adjacent)) continue;
                if (adjacent == tileId) return current.Depth + 1;
                queue.Enqueue((adjacent, current.Depth + 1));
            }
        }

        return 0;
    }

    private static TileStateKind ToTileStateKind(GameState state, TileState tile, BuildingDefinition? definition = null)
    {
        return tile.Ownership switch
        {
            TileOwnership.Locked => TileStateKind.Locked,
            TileOwnership.Unlockable => TileStateKind.Unlockable,
            TileOwnership.Unlocked when definition is null => TileStateKind.Unlocked,
            TileOwnership.Unlocked when definition.RequiredTerrain != tile.Terrain => TileStateKind.Unlocked,
            TileOwnership.Unlocked when state.Buildings.Values.Count(b => b.TileId == tile.TileId) >= tile.MaxBuildingSlots => TileStateKind.Unlocked,
            TileOwnership.Unlocked => TileStateKind.Buildable,
            _ => TileStateKind.Locked
        };
    }

    private static CommandRejectedEvent Reject(ValidationReasonCode code) => new(code, ValidationMessage(code));

    private static ExpansionValidationResult InvalidExpansion(ValidationReasonCode code, int cost, int depth, int nextUnlockIndex, TileStateKind tileState)
        => new(false, code, ValidationMessage(code), cost, depth, nextUnlockIndex, tileState);

    private static PlacementValidationResult InvalidPlacement(ValidationReasonCode code, int cost, TileStateKind tileState)
        => new(false, code, ValidationMessage(code), cost, tileState);

    private sealed record BuildingDefinition(
        string BuildingTypeId,
        int BaseCost,
        int BaseProductionPerTick,
        int MaxLevel,
        int[] UpgradeCosts,
        string? RequiredUnlockFlag,
        TerrainType RequiredTerrain);
}
