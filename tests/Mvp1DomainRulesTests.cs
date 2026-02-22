using LandBuilder.Domain;

namespace LandBuilder.Tests;

public static class Mvp1DomainRulesTests
{
    public static void PlaceBuildingConsumesCoinsAndAddsBuilding()
    {
        var state = GameState.CreateMvp0Default();
        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0));

        if (result.State.Buildings.Count != 1) throw new Exception("Expected one building");
        if (result.State.Economy.Coins != 13) throw new Exception("Expected 13 coins after placing camp");
    }

    public static void PlaceBuildingFailsWhenTileSlotsFull()
    {
        var state = GameState.CreateMvp0Default();
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0));

        if (result.State.Buildings.Count != 1) throw new Exception("Unexpected extra building");
        if (result.Events.OfType<CommandRejectedEvent>().FirstOrDefault() is null)
        {
            throw new Exception("Expected command rejection");
        }
    }

    public static void TickGeneratesCoinsFromBuildings()
    {
        var state = GameState.CreateMvp0Default();
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;

        var result = DeterministicSimulator.Apply(state, new TickCommand(4));
        if (result.State.Economy.Coins != 17) throw new Exception("Expected +4 coins from tick");
    }
}
