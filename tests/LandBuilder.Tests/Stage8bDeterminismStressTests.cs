using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8bDeterminismStressTests
{
    [Fact]
    public void DeterminismStress_MixedBuildingsAndUpgrades_ProducesIdenticalHashes()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var hashes = new List<string>();

        for (var run = 0; run < 5; run++)
        {
            var state = GameState.CreateInitial(objectives);
            state = state with { Economy = state.Economy with { Coins = 500 } };

            state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
            state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
            state = state with
            {
                Progression = state.Progression with { UnlockFlags = state.Progression.UnlockFlags.Append("UNLOCK_QUARRY").ToHashSet() }
            };

            state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
            state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;
            state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Sawmill", 1)).State;
            state = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(1)).State;
            state = DeterministicSimulator.Apply(state, new TickCommand(5000)).State;

            hashes.Add(Hash(state));
        }

        Assert.True(hashes.All(h => h == hashes[0]));
    }

    private static string Hash(GameState state)
    {
        var contributions = DeterministicSimulator.GetBuildingContributions(state);
        var serialized = string.Join("|", contributions.Select(c => $"{c.BuildingId}:{c.BuildingTypeId}:{c.Level}:{c.Contribution}"))
                         + $"|coins:{state.Economy.Coins}|life:{state.Economy.LifetimeCoinsEarned}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }
}
