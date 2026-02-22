using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8aExpansionCostTests
{
    [Fact]
    public void ExpansionPreviewAndActualCost_MatchAndScaleMonotonically()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);

        var preview1 = DeterministicSimulator.ValidateExpansion(state, 1);
        Assert.True(preview1.IsValid);

        var afterExpand1 = DeterministicSimulator.Apply(state, new ExpandTileCommand(1));
        var spent1 = Assert.IsType<CurrencySpentEvent>(afterExpand1.Events.First(e => e is CurrencySpentEvent));
        Assert.Equal(preview1.Cost, spent1.Amount);

        state = afterExpand1.State;
        var preview2 = DeterministicSimulator.ValidateExpansion(state, 2);
        Assert.True(preview2.IsValid);
        Assert.True(preview2.Cost >= preview1.Cost);

        var afterExpand2 = DeterministicSimulator.Apply(state, new ExpandTileCommand(2));
        var spent2 = Assert.IsType<CurrencySpentEvent>(afterExpand2.Events.First(e => e is CurrencySpentEvent));
        Assert.Equal(preview2.Cost, spent2.Amount);
    }

    [Fact]
    public void DeterminismReplay_WithExpansionsAndPlacementValidation_IsStable()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var commands = new IGameCommand[]
        {
            new ExpandTileCommand(1),
            new PlaceBuildingCommand("Camp", 0),
            new TickCommand(4),
            new ExpandTileCommand(2),
            new TickCommand(3)
        };

        var finalA = ReplayWithPreview(commands, objectives);
        var finalB = ReplayWithPreview(commands, objectives);

        Assert.Equal(finalA.Economy.Coins, finalB.Economy.Coins);
        Assert.Equal(finalA.World.Tiles[2].Ownership, finalB.World.Tiles[2].Ownership);
        Assert.Equal(finalA.Progression.CurrentObjectiveIndex, finalB.Progression.CurrentObjectiveIndex);
    }

    [Fact]
    public void SaveLoadRoundTrip_PreservesExpandedWorldAndExpansionPreviewCosts()
    {
        var savePath = Path.Combine(Path.GetTempPath(), $"landbuilder_stage8a_{Guid.NewGuid():N}.json");
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);

        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;

        var previewBefore = DeterministicSimulator.ValidateExpansion(state, 2);
        repo.Save(savePath, state);
        var loaded = repo.Load(savePath);
        var previewAfter = DeterministicSimulator.ValidateExpansion(loaded, 2);

        Assert.Equal(TileOwnership.Unlocked, loaded.World.Tiles[1].Ownership);
        Assert.Equal(TileOwnership.Unlockable, loaded.World.Tiles[2].Ownership);
        Assert.Equal(previewBefore.Cost, previewAfter.Cost);

        File.Delete(savePath);
    }

    private static GameState ReplayWithPreview(IEnumerable<IGameCommand> commands, IReadOnlyList<ObjectiveDefinition> objectives)
    {
        var state = GameState.CreateInitial(objectives);
        foreach (var command in commands)
        {
            if (command is ExpandTileCommand expand)
            {
                _ = DeterministicSimulator.ValidateExpansion(state, expand.TileId);
            }

            state = DeterministicSimulator.Apply(state, command).State;
        }

        return state;
    }
}
