using System.Collections.Generic;
using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10cEconomyPacingTests
{
    [Fact]
    public void ExpansionCosts_AreMonotonicByDepth_AndNonDecreasingWithUnlockIndex()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var baseState = GameState.CreateInitial(objectives) with { Economy = new EconomyState(1000, 0) };
        var baseTiles = baseState.World.Tiles.ToDictionary(x => x.Key, x => x.Value);

        var lowerDepthTiles = baseTiles.ToDictionary(x => x.Key, x => x.Value);
        lowerDepthTiles[1] = lowerDepthTiles[1] with { Ownership = TileOwnership.Unlockable, RegionDepth = 1 };
        var lowerDepthState = baseState with { World = new WorldState(lowerDepthTiles) };

        var higherDepthTiles = baseTiles.ToDictionary(x => x.Key, x => x.Value);
        higherDepthTiles[1] = higherDepthTiles[1] with { Ownership = TileOwnership.Unlockable, RegionDepth = 2 };
        var higherDepthState = baseState with { World = new WorldState(higherDepthTiles) };

        var depth1Cost = DeterministicSimulator.ValidateExpansion(lowerDepthState, 1).Cost;
        var depth2Cost = DeterministicSimulator.ValidateExpansion(higherDepthState, 1).Cost;

        Assert.True(depth2Cost >= depth1Cost);

        var higherIndexTiles = lowerDepthState.World.Tiles.ToDictionary(x => x.Key, x => x.Value);
        higherIndexTiles[3] = higherIndexTiles[3] with { Ownership = TileOwnership.Unlocked };
        higherIndexTiles[1] = higherIndexTiles[1] with { Ownership = TileOwnership.Unlockable, RegionDepth = 1 };
        var higherIndexState = lowerDepthState with { World = new WorldState(higherIndexTiles) };

        var lowIndex = DeterministicSimulator.ValidateExpansion(lowerDepthState, 1).Cost;
        var highIndex = DeterministicSimulator.ValidateExpansion(higherIndexState, 1).Cost;
        Assert.True(highIndex >= lowIndex);
    }

    [Fact]
    public void UpgradeCosts_AreMonotonicByLevel_AndProductionEqualsSumOfContributions()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(2000, 0) };
        state = state with
        {
            Progression = state.Progression with
            {
                UnlockFlags = state.Progression.UnlockFlags.Append("UNLOCK_QUARRY").ToHashSet()
            }
        };

        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;

        var up1 = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(1));
        var cost1 = Assert.IsType<CurrencySpentEvent>(up1.Events.First(e => e is CurrencySpentEvent)).Amount;
        var up2 = DeterministicSimulator.Apply(up1.State, new UpgradeBuildingCommand(1));
        var cost2 = Assert.IsType<CurrencySpentEvent>(up2.Events.First(e => e is CurrencySpentEvent)).Amount;

        Assert.True(cost2 >= cost1);

        var contributions = DeterministicSimulator.GetBuildingContributions(up2.State);
        Assert.Equal(contributions.Sum(c => c.Contribution), DeterministicSimulator.GetProductionPerTick(up2.State));
    }
}
