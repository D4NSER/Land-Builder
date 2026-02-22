using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;

namespace LandBuilder.Tests;

public static class Mvp2ProgressionRulesTests
{
    public static void SixStepObjectiveChainCompletesDeterministically()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var state = GameState.CreateInitial(objectives);

        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;           // Obj1
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State; // Obj2
        state = DeterministicSimulator.Apply(state, new TickCommand(20)).State;                  // Obj3
        state = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(1)).State;        // Obj4 unlock quarry
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State; // Obj5
        state = DeterministicSimulator.Apply(state, new TickCommand(1)).State;                    // Obj6

        if (state.Progression.CurrentObjectiveIndex != 6)
            throw new Exception("Expected all 6 objectives completed");
    }

    public static void QuarryPlacementBlockedBeforeUnlockFlag()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;

        var result = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2));
        if (result.Events.OfType<CommandRejectedEvent>().FirstOrDefault() is null)
            throw new Exception("Expected unlock-flag rejection for quarry");
    }
}
