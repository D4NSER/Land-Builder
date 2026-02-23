using System.Security.Cryptography;
using System.Text;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp3DeterminismStressTests
{
    [Fact]
    public void DeterminismStress_10000Ticks_FixedStream_Repeated5Runs_HasIdenticalStateHash()
    {
        const int tickCount = 10_000;
        const int runCount = 5;

        string? baseline = null;
        for (var run = 0; run < runCount; run++)
        {
            var state = ExecuteScenario(tickCount);
            var hash = ComputeStateHash(state);

            baseline ??= hash;
            Assert.Equal(baseline, hash);
        }
    }

    private static GameState ExecuteScenario(int tickCount)
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);

        // Fixed command stream setup.
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new TickCommand(20)).State;
        state = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;

        // Stress run with deterministic cadence.
        for (var i = 1; i <= tickCount; i++)
        {
            state = DeterministicSimulator.Apply(state, new TickCommand(1)).State;

            if (i == 3_000)
                state = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(1)).State;
        }

        return state;
    }

    private static string ComputeStateHash(GameState state)
    {
        var payload = new StringBuilder();
        payload.Append(state.Economy.Coins).Append('|')
            .Append(state.Economy.LifetimeCoinsEarned).Append('|')
            .Append(state.Progression.CurrentObjectiveIndex).Append('|');

        foreach (var tile in state.World.Tiles.OrderBy(p => p.Key).Select(p => p.Value))
            payload.Append($"T:{tile.TileId}:{(int)tile.Ownership}:{tile.UnlockCost};");

        foreach (var building in state.Buildings.OrderBy(p => p.Key).Select(p => p.Value))
            payload.Append($"B:{building.BuildingId}:{building.BuildingTypeId}:{building.TileId}:{building.Level};");

        foreach (var flag in state.Progression.UnlockFlags.OrderBy(x => x))
            payload.Append($"F:{flag};");

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()));
        return Convert.ToHexString(bytes);
    }
}
