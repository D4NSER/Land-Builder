using System.Security.Cryptography;
using System.Text;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp3SaveLoadStabilityTests
{
    [Fact]
    public void SaveLoadStability_50RoundTrips_NoStateDrift()
    {
        const int cycleCount = 50;
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);
        var repo = new SaveRepository(objectives);
        var savePath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp3_stability_save.json");

        // Establish a non-trivial baseline state.
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new TickCommand(20)).State;
        state = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;

        for (var i = 0; i < cycleCount; i++)
        {
            state = DeterministicSimulator.Apply(state, new TickCommand(5)).State;
            var beforeHash = ComputeStateHash(state);

            repo.Save(savePath, state);
            var loaded = repo.Load(savePath);
            var afterHash = ComputeStateHash(loaded);

            Assert.Equal(beforeHash, afterHash);
            state = loaded;
        }

        File.Delete(savePath);
    }

    private static string ComputeStateHash(GameState state)
    {
        var payload = new StringBuilder();
        payload.Append(state.Economy.Coins).Append('|')
            .Append(state.Economy.LifetimeCoinsEarned).Append('|')
            .Append(state.Progression.CurrentObjectiveIndex).Append('|')
            .Append(state.Progression.LastCompletedObjectiveId).Append('|')
            .Append(state.Progression.LastCompletionMessage).Append('|');

        foreach (var tile in state.World.Tiles.OrderBy(p => p.Key).Select(p => p.Value))
            payload.Append($"T:{tile.TileId}:{(int)tile.Ownership}:{tile.UnlockCost};");

        foreach (var building in state.Buildings.OrderBy(p => p.Key).Select(p => p.Value))
            payload.Append($"B:{building.BuildingId}:{building.BuildingTypeId}:{building.TileId}:{building.Level};");

        foreach (var objective in state.Progression.CompletedObjectiveIds)
            payload.Append($"O:{objective};");

        foreach (var flag in state.Progression.UnlockFlags.OrderBy(x => x))
            payload.Append($"F:{flag};");

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()));
        return Convert.ToHexString(bytes);
    }
}
