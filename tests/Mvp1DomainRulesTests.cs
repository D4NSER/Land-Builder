using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;

namespace LandBuilder.Tests;

public static class Mvp1DomainRulesTests
{
    public static void PlaceBuildingConsumesCoinsAndAddsBuilding()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var state = GameState.CreateInitial(objectives);
        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0));

        if (result.State.Buildings.Count != 1) throw new Exception("Expected one building");
        if (result.State.Economy.Coins != 18) throw new Exception("Expected 18 coins after placing camp and objective reward");
    }

    public static void PlaceBuildingFailsWhenTileSlotsFull()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0));

        if (result.State.Buildings.Count != 1) throw new Exception("Unexpected extra building");
        if (result.Events.OfType<CommandRejectedEvent>().FirstOrDefault() is null)
            throw new Exception("Expected command rejection");
    }

    public static void TickGeneratesCoinsFromBuildings()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;

        var result = DeterministicSimulator.Apply(state, new TickCommand(4));
        if (result.State.Economy.Coins <= state.Economy.Coins) throw new Exception("Expected tick to increase coins");
    }
}
