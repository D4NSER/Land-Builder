using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10bProductionDeterminismTests
{
    [Fact]
    public void MixedBuildings_Upgrades_10000Ticks_K5_HasIdenticalEndStateHash()
    {
        var hashes = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var state = Replay();
            hashes.Add(Hash(state));
        }

        Assert.True(hashes.All(h => h == hashes[0]));
    }

    private static GameState Replay()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(2000, 0) };
        state = state with
        {
            Progression = state.Progression with
            {
                UnlockFlags = state.Progression.UnlockFlags.Append("UNLOCK_QUARRY").ToHashSet()
            }
        };

        var commands = new IGameCommand[]
        {
            new ExpandTileCommand(1), new ExpandTileCommand(2), new ExpandTileCommand(3),
            new ExpandTileCommand(4), new ExpandTileCommand(5),
            new PlaceBuildingCommand("Camp", 0),
            new PlaceBuildingCommand("Quarry", 2),
            new PlaceBuildingCommand("Sawmill", 4),
            new PlaceBuildingCommand("Forester", 3),
            new PlaceBuildingCommand("ClayWorks", 5),
            new UpgradeBuildingCommand(3),
            new UpgradeBuildingCommand(4),
            new UpgradeBuildingCommand(5),
            new TickCommand(10000)
        };

        foreach (var command in commands)
            state = DeterministicSimulator.Apply(state, command).State;

        return state;
    }

    private static string Hash(GameState state)
    {
        var contributions = DeterministicSimulator.GetBuildingContributions(state);
        var payload = string.Join("|", contributions.Select(c => $"{c.BuildingId}:{c.BuildingTypeId}:{c.Level}:{c.Contribution}"))
                      + $"|coins:{state.Economy.Coins}|life:{state.Economy.LifetimeCoinsEarned}|obj:{state.Progression.CurrentObjectiveIndex}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
