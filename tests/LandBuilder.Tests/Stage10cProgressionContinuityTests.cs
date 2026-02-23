using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10cProgressionContinuityTests
{
    [Fact]
    public void FixedCommandStream_CompletesFullChain_DeterministicallyWithoutDeadlock()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(3000, 0) };

        var stream = new IGameCommand[]
        {
            new ExpandTileCommand(1),
            new PlaceBuildingCommand("Camp", 0),
            new TickCommand(20),
            new UpgradeBuildingCommand(1),
            new ExpandTileCommand(2),
            new PlaceBuildingCommand("Quarry", 2),
            new TickCommand(1),
            new ExpandTileCommand(3),
            new ExpandTileCommand(4),
            new ExpandTileCommand(5),
            new PlaceBuildingCommand("Sawmill", 4),
            new UpgradeBuildingCommand(3),
            new TickCommand(40)
        };

        foreach (var cmd in stream)
            state = DeterministicSimulator.Apply(state, cmd).State;

        Assert.Equal(14, state.Progression.CurrentObjectiveIndex);
        Assert.Equal(14, state.Progression.CompletedObjectiveIds.Count);
        Assert.Equal("OBJ_14_CH2_MIXED_ONE_EACH", state.Progression.LastCompletedObjectiveId);
    }
}
