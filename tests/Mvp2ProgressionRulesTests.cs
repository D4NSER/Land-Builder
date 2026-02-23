using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp2ProgressionRulesTests
{
    [Fact]
    public void SixStepObjectiveChainCompletesDeterministically()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var state = GameState.CreateInitial(objectives);

        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new TickCommand(20)).State;
        state = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;
        state = DeterministicSimulator.Apply(state, new TickCommand(1)).State;

        Assert.Equal(6, state.Progression.CurrentObjectiveIndex);
    }

    [Fact]
    public void QuarryPlacementBlockedBeforeUnlockFlag()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;

        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2));
        Assert.Contains(result.Events, e => e is CommandRejectedEvent);
    }
}
