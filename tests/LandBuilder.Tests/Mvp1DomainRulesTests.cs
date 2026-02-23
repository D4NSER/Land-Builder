using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp1DomainRulesTests
{
    [Fact]
    public void PlaceBuildingConsumesCoinsAndAddsBuilding()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);
        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0));

        Assert.Equal(1, result.State.Buildings.Count);
        Assert.Equal(18, result.State.Economy.Coins);
    }

    [Fact]
    public void PlaceBuildingFailsWhenTileSlotsFull()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0));

        Assert.Single(result.State.Buildings);
        Assert.Contains(result.Events, e => e is CommandRejectedEvent);
    }

    [Fact]
    public void TickGeneratesCoinsFromBuildings()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;

        var result = DeterministicSimulator.Apply(state, new TickCommand(4));
        Assert.True(result.State.Economy.Coins > state.Economy.Coins);
    }
}
