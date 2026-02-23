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
        var placementCost = DeterministicSimulator.ValidatePlacement(state, "Camp", 0).Cost;
        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0));

        Assert.Single(result.State.Buildings);
        Assert.Equal(state.Economy.Coins - placementCost, result.State.Economy.Coins);
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
