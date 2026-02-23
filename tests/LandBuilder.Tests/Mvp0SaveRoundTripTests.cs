using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp0SaveRoundTripTests
{
    [Fact]
    public void SaveLoadRoundTripPreservesState()
    {
        var savePath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp2_test_save.json");
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);

        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new TickCommand(3)).State;

        var repo = new SaveRepository(objectives);
        repo.Save(savePath, state);
        var loaded = repo.Load(savePath);

        Assert.Equal(state.Economy.Coins, loaded.Economy.Coins);
        Assert.Equal(state.Buildings.Count, loaded.Buildings.Count);
        Assert.Equal(state.Progression.CurrentObjectiveIndex, loaded.Progression.CurrentObjectiveIndex);
        Assert.Equal(state.World.Tiles[8].RegionDepth, loaded.World.Tiles[8].RegionDepth);
        Assert.Equal(3, loaded.Meta.SchemaVersion);

        File.Delete(savePath);
    }
}
