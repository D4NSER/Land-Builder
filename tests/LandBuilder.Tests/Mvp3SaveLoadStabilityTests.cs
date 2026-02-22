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
        var backupPath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp3_stability_save.backup.json");

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

            repo.Save(savePath, state, backupPath);
            var loaded = repo.Load(savePath);
            var afterHash = ComputeStateHash(loaded);

            Assert.Equal(beforeHash, afterHash);
            state = loaded;
        }

        File.Delete(savePath);
        File.Delete(backupPath);
    }

    [Fact]
    public void LoadWithRecovery_UsesBackupWhenPrimaryIsCorrupt()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);
        var repo = new SaveRepository(objectives);

        var primaryPath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp3_primary_corrupt.json");
        var backupPath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp3_backup_valid.json");

        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        repo.Save(backupPath, state, null);
        File.WriteAllText(primaryPath, "{ definitely-not-json }");

        var recovered = repo.LoadWithRecovery(primaryPath, backupPath, () => GameState.CreateInitial(objectives));

        Assert.Equal("Primary save was invalid. Loaded backup save.", recovered.StatusMessage);
        Assert.Equal(state.Economy.Coins, recovered.State.Economy.Coins);

        File.Delete(primaryPath);
        File.Delete(backupPath);
    }

    [Fact]
    public void LoadWithRecovery_UsesSafeDefaultWhenPrimaryAndBackupInvalid()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);

        var primaryPath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp3_primary_invalid.json");
        var backupPath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp3_backup_invalid.json");

        File.WriteAllText(primaryPath, "bad");
        File.WriteAllText(backupPath, "also bad");

        var recovered = repo.LoadWithRecovery(primaryPath, backupPath, () => GameState.CreateInitial(objectives));

        Assert.Equal("Primary and backup saves were unavailable. Started a safe default session.", recovered.StatusMessage);
        Assert.Equal(0, recovered.State.Progression.CurrentObjectiveIndex);

        File.Delete(primaryPath);
        File.Delete(backupPath);
    }

    [Fact]
    public void ObjectiveLoader_ErrorMessage_IsActionableOnMissingFile()
    {
        var loader = new ObjectiveDefinitionLoader();
        var missingPath = Path.Combine(Path.GetTempPath(), "does_not_exist_objectives.json");

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load(missingPath));
        Assert.Contains("Objective file not found", ex.Message);
        Assert.Contains("mvp2_objectives.json", ex.Message);
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
