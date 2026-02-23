using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10cSaveLoadContinuityTests
{
    [Fact]
    public void RoundTrip_PreservesEconomyProgression_AndProductionAfterTuning()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);
        var path = Path.Combine(Path.GetTempPath(), $"stage10c_roundtrip_{Guid.NewGuid():N}.json");

        var state = BuildTunedScenario(objectives);
        var productionBefore = DeterministicSimulator.GetProductionPerTick(state);

        repo.Save(path, state);
        var loaded = repo.Load(path);

        Assert.Equal(state.Economy.Coins, loaded.Economy.Coins);
        Assert.Equal(state.Economy.LifetimeCoinsEarned, loaded.Economy.LifetimeCoinsEarned);
        Assert.Equal(state.Progression.CurrentObjectiveIndex, loaded.Progression.CurrentObjectiveIndex);
        Assert.Equal(state.Progression.CompletedObjectiveIds, loaded.Progression.CompletedObjectiveIds);
        Assert.Equal(state.Progression.LastCompletedObjectiveId, loaded.Progression.LastCompletedObjectiveId);
        Assert.Equal(state.Buildings.Count, loaded.Buildings.Count);
        Assert.Equal(state.World.Tiles.Count, loaded.World.Tiles.Count);
        Assert.Equal(productionBefore, DeterministicSimulator.GetProductionPerTick(loaded));

        File.Delete(path);
    }

    [Fact]
    public void LoadWithRecovery_CorruptPrimary_UsesBackupWithoutRegression()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);
        var primaryPath = Path.Combine(Path.GetTempPath(), $"stage10c_primary_{Guid.NewGuid():N}.json");
        var backupPath = Path.Combine(Path.GetTempPath(), $"stage10c_backup_{Guid.NewGuid():N}.json");

        var backupState = BuildTunedScenario(objectives);
        repo.Save(primaryPath, backupState, backupPath);

        File.WriteAllText(primaryPath, "this is intentionally corrupt");
        var (recovered, status) = repo.LoadWithRecovery(primaryPath, backupPath, () => GameState.CreateInitial(objectives));

        Assert.Equal("Primary save was invalid. Loaded backup save.", status);
        Assert.Equal(backupState.Economy.Coins, recovered.Economy.Coins);
        Assert.Equal(backupState.Progression.CurrentObjectiveIndex, recovered.Progression.CurrentObjectiveIndex);
        Assert.Equal(backupState.Progression.CompletedObjectiveIds, recovered.Progression.CompletedObjectiveIds);
        Assert.Equal(backupState.World.Tiles.Count(t => t.Value.Ownership == TileOwnership.Unlocked), recovered.World.Tiles.Count(t => t.Value.Ownership == TileOwnership.Unlocked));

        File.Delete(primaryPath);
        File.Delete(backupPath);
    }

    private static GameState BuildTunedScenario(IReadOnlyList<ObjectiveDefinition> objectives)
    {
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(4000, 0) };

        var commands = new IGameCommand[]
        {
            new ExpandTileCommand(1),
            new PlaceBuildingCommand("Camp", 0),
            new TickCommand(15),
            new UpgradeBuildingCommand(1),
            new ExpandTileCommand(2),
            new PlaceBuildingCommand("Quarry", 2),
            new ExpandTileCommand(3),
            new ExpandTileCommand(4),
            new PlaceBuildingCommand("Sawmill", 4),
            new PlaceBuildingCommand("Forester", 3),
            new ExpandTileCommand(5),
            new PlaceBuildingCommand("ClayWorks", 5),
            new UpgradeBuildingCommand(3),
            new TickCommand(30)
        };

        foreach (var command in commands)
            state = DeterministicSimulator.Apply(state, command).State;

        return state;
    }
}
