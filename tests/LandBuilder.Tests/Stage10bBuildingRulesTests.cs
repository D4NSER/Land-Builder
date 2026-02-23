using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10bBuildingRulesTests
{
    [Fact]
    public void ForesterAndClayWorks_GatingMatrix_UsesDeterministicReasonCodesAndMessages()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(1000, 0) };

        var foresterBlocked = DeterministicSimulator.ValidatePlacement(state, "Forester", 3);
        Assert.False(foresterBlocked.IsValid);
        Assert.Equal(ValidationReasonCode.MissingPrerequisiteBuildingCount, foresterBlocked.ReasonCode);
        Assert.Equal("Requires at least 1 Sawmill", foresterBlocked.Message);

        var clayBlocked = DeterministicSimulator.ValidatePlacement(state, "ClayWorks", 5);
        Assert.False(clayBlocked.IsValid);
        Assert.Equal(ValidationReasonCode.BuildingTypeNotUnlocked, clayBlocked.ReasonCode);

        state = state with
        {
            Progression = state.Progression with
            {
                UnlockFlags = state.Progression.UnlockFlags.Append("UNLOCK_QUARRY").ToHashSet()
            }
        };

        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(3)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(4)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(5)).State;

        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Sawmill", 4)).State;

        var foresterAllowed = DeterministicSimulator.ValidatePlacement(state, "Forester", 3);
        Assert.True(foresterAllowed.IsValid);
        Assert.Equal(ValidationReasonCode.None, foresterAllowed.ReasonCode);

        var clayAllowed = DeterministicSimulator.ValidatePlacement(state, "ClayWorks", 5);
        Assert.True(clayAllowed.IsValid);
        Assert.Equal(ValidationReasonCode.None, clayAllowed.ReasonCode);

        var wrongTerrain = DeterministicSimulator.ValidatePlacement(state, "Forester", 4);
        Assert.False(wrongTerrain.IsValid);
        Assert.Equal(ValidationReasonCode.TerrainMismatch, wrongTerrain.ReasonCode);
    }
}
