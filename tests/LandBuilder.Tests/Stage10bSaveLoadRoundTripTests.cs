using System;
using System.IO;
using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10bSaveLoadRoundTripTests
{
    [Fact]
    public void SaveLoadRoundTrip_PreservesMixedBuildings_AndProductionParity()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);
        var path = Path.Combine(Path.GetTempPath(), $"stage10b_roundtrip_{Guid.NewGuid():N}.json");

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
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(3)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(4)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(5)).State;

        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Sawmill", 4)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Forester", 3)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("ClayWorks", 5)).State;

        var before = DeterministicSimulator.GetProductionPerTick(state);
        repo.Save(path, state);
        var loaded = repo.Load(path);
        var after = DeterministicSimulator.GetProductionPerTick(loaded);

        Assert.Equal(5, loaded.Buildings.Count);
        Assert.Equal(before, after);

        File.Delete(path);
    }
}
