using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8cSaveLoadProgressionTests
{
    [Fact]
    public void PartialChapter2Progress_RoundTripPreservesProgressionExactly()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);
        var path = Path.Combine(Path.GetTempPath(), $"stage8c_partial_{Guid.NewGuid():N}.json");

        var state = BuildScenarioToObjectiveIndexAtLeast(10, objectives);
        repo.Save(path, state);
        var loaded = repo.Load(path);

        Assert.Equal(state.Progression.CurrentObjectiveIndex, loaded.Progression.CurrentObjectiveIndex);
        Assert.Equal(state.Progression.LastCompletedObjectiveId, loaded.Progression.LastCompletedObjectiveId);
        Assert.Equal(state.Progression.CompletedObjectiveIds, loaded.Progression.CompletedObjectiveIds);
        Assert.Equal(state.Economy.LifetimeCoinsEarned, loaded.Economy.LifetimeCoinsEarned);

        File.Delete(path);
    }

    [Fact]
    public void FullChapter2Progress_RoundTripPreservesCompletedChainExactly()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);
        var path = Path.Combine(Path.GetTempPath(), $"stage8c_full_{Guid.NewGuid():N}.json");

        var state = BuildScenarioToObjectiveIndexAtLeast(14, objectives);
        repo.Save(path, state);
        var loaded = repo.Load(path);

        Assert.Equal(14, loaded.Progression.CurrentObjectiveIndex);
        Assert.Equal("OBJ_14_CH2_MIXED_ONE_EACH", loaded.Progression.LastCompletedObjectiveId);
        Assert.Equal(state.Progression.CompletedObjectiveIds, loaded.Progression.CompletedObjectiveIds);
        Assert.Equal(state.Economy.Coins, loaded.Economy.Coins);

        File.Delete(path);
    }

    private static GameState BuildScenarioToObjectiveIndexAtLeast(int minimumIndex, IReadOnlyList<ObjectiveDefinition> objectives)
    {
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(500, 0) };
        var commands = new IGameCommand[]
        {
            new ExpandTileCommand(1),
            new PlaceBuildingCommand("Camp", 0),
            new TickCommand(20),
            new UpgradeBuildingCommand(1),
            new ExpandTileCommand(2),
            new PlaceBuildingCommand("Quarry", 2),
            new TickCommand(1),
            new PlaceBuildingCommand("Sawmill", 1),
            new UpgradeBuildingCommand(3),
            new TickCommand(30)
        };

        foreach (var command in commands)
        {
            state = DeterministicSimulator.Apply(state, command).State;
            if (state.Progression.CurrentObjectiveIndex >= minimumIndex)
                break;
        }

        return state;
    }
}
