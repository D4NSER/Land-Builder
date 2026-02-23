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

        var depth1Cost = DeterministicSimulator.ValidateExpansion(baseState, 1).Cost;
        var after1 = DeterministicSimulator.Apply(baseState, new ExpandTileCommand(1)).State;
        var depth2Cost = DeterministicSimulator.ValidateExpansion(after1, 2).Cost;
        var after3 = DeterministicSimulator.Apply(baseState, new ExpandTileCommand(3)).State;
        var depth3Cost = DeterministicSimulator.ValidateExpansion(after3, 6).Cost;

        Assert.True(depth2Cost >= depth1Cost);
        Assert.True(depth3Cost >= depth2Cost);

        var tile2 = after1.World.Tiles[2];
        var syntheticTiles = after1.World.Tiles.ToDictionary(x => x.Key, x => x.Value);
        syntheticTiles[3] = syntheticTiles[3] with { Ownership = TileOwnership.Unlocked };
        syntheticTiles[4] = syntheticTiles[4] with { Ownership = TileOwnership.Unlocked };
        syntheticTiles[2] = tile2 with { Ownership = TileOwnership.Unlockable };
        var higherIndexState = after1 with { World = new WorldState(syntheticTiles) };

        var lowIndex = DeterministicSimulator.ValidateExpansion(after1, 2).Cost;
        var highIndex = DeterministicSimulator.ValidateExpansion(higherIndexState, 2).Cost;
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
