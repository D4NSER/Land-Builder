using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8cProgressionDeterminismTests
{
    [Fact]
    public void Chapter2DeterminismReplay_K5_CompletesWithIdenticalMetadataAndStateHash()
    {
        var hashes = new List<string>();
        var indices = new List<int>();
        var completionIds = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            var state = ReplayChapter2Scenario();
            hashes.Add(Hash(state));
            indices.Add(state.Progression.CurrentObjectiveIndex);
            completionIds.Add(state.Progression.LastCompletedObjectiveId);
        }

        Assert.True(hashes.All(h => h == hashes[0]));
        Assert.True(indices.All(i => i == 14));
        Assert.True(completionIds.All(id => id == "OBJ_14_CH2_MIXED_ONE_EACH"));
    }

    private static GameState ReplayChapter2Scenario()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
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
            state = DeterministicSimulator.Apply(state, command).State;

        return state;
    }

    private static string Hash(GameState state)
    {
        var contributions = DeterministicSimulator.GetBuildingContributions(state);
        var data = string.Join("|", contributions.Select(c => $"{c.BuildingId}:{c.BuildingTypeId}:{c.Level}:{c.Contribution}")) +
                   $"|coins:{state.Economy.Coins}|lifetime:{state.Economy.LifetimeCoinsEarned}|idx:{state.Progression.CurrentObjectiveIndex}|last:{state.Progression.LastCompletedObjectiveId}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}
