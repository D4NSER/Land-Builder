using System;
using System.IO;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10aSaveLoadMapTests
{
    [Fact]
    public void SaveLoadRoundTrip_PreservesExpandedOwnershipAndRegionDepthMetadata()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);
        var savePath = Path.Combine(Path.GetTempPath(), $"landbuilder_stage10a_{Guid.NewGuid():N}.json");

        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(500, 0) };
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(3)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(4)).State;

        repo.Save(savePath, state);
        var loaded = repo.Load(savePath);

        Assert.Equal(TileOwnership.Unlocked, loaded.World.Tiles[4].Ownership);
        Assert.Equal(state.World.Tiles[8].RegionDepth, loaded.World.Tiles[8].RegionDepth);
        Assert.Equal(state.World.Tiles[5].RegionDepth, loaded.World.Tiles[5].RegionDepth);

        File.Delete(savePath);
    }
}
