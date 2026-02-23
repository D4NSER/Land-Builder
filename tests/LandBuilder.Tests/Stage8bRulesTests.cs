using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8bRulesTests
{
    [Fact]
    public void SawmillPlacement_IsGatedByQuarryCount_WithDeterministicReasonCode()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);

        var blocked = DeterministicSimulator.ValidatePlacement(state, "Sawmill", 0);
        Assert.False(blocked.IsValid);
        Assert.Equal(ValidationReasonCode.MissingPrerequisiteBuildingCount, blocked.ReasonCode);
        Assert.Equal("Requires at least 1 Quarry", blocked.Message);

        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        state = state with
        {
            Economy = state.Economy with { Coins = 300 },
            Progression = state.Progression with { UnlockFlags = state.Progression.UnlockFlags.Append("UNLOCK_QUARRY").ToHashSet() }
        };

        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;

        var allowed = DeterministicSimulator.ValidatePlacement(state, "Sawmill", 0);
        Assert.True(allowed.IsValid);
        Assert.Equal(ValidationReasonCode.None, allowed.ReasonCode);
    }
}
