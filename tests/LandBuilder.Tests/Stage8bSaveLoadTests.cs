using System;
using System.IO;
using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8bSaveLoadTests
{
    [Fact]
    public void SaveLoadRoundTrip_PreservesMixedBuildings_AndPostLoadProduction()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);
        var savePath = Path.Combine(Path.GetTempPath(), $"landbuilder_stage8b_{Guid.NewGuid():N}.json");

        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(500, 0) };
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        state = state with
        {
            Progression = state.Progression with { UnlockFlags = state.Progression.UnlockFlags.Append("UNLOCK_QUARRY").ToHashSet() }
        };

        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Sawmill", 1)).State;

        // Resolve any immediately pending progression reward before save so post-load tick delta
        // assertions reflect tick production only (no objective reward side-effects).
        state = DeterministicSimulator.Apply(state, new TickCommand(2)).State;

        var productionBefore = DeterministicSimulator.GetProductionPerTick(state);
        repo.Save(savePath, state);

        var loaded = repo.Load(savePath);
        var productionAfterLoad = DeterministicSimulator.GetProductionPerTick(loaded);

        var afterSingleTick = DeterministicSimulator.Apply(loaded, new TickCommand(1)).State;
        var afterTenTicks = DeterministicSimulator.Apply(loaded, new TickCommand(10)).State;

        Assert.Equal(3, loaded.Buildings.Count);
        Assert.Equal(state.Progression.CurrentObjectiveIndex, loaded.Progression.CurrentObjectiveIndex);
        Assert.Equal(state.Economy.LifetimeCoinsEarned, loaded.Economy.LifetimeCoinsEarned);
        Assert.Equal(productionBefore, productionAfterLoad);
        Assert.Equal(loaded.Economy.Coins + productionAfterLoad, afterSingleTick.Economy.Coins);
        Assert.Equal(loaded.Economy.Coins + (productionAfterLoad * 10), afterTenTicks.Economy.Coins);

        File.Delete(savePath);
    }
}
