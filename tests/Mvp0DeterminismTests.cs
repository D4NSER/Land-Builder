using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;

namespace LandBuilder.Tests;

public static class Mvp0DeterminismTests
{
    public static void SameInputsProduceSameOutputs()
    {
        var commands = new IGameCommand[]
        {
            new ExpandTileCommand(1),
            new PlaceBuildingCommand("Camp", 0),
            new TickCommand(5),
            new UpgradeBuildingCommand(1),
            new TickCommand(3),
            new ExpandTileCommand(2),
            new PlaceBuildingCommand("Quarry", 2),
            new TickCommand(2)
        };

        var a = Replay(commands);
        var b = Replay(commands);

        if (a.Economy.Coins != b.Economy.Coins) throw new Exception("Coins diverged");
        if (a.Progression.CurrentObjectiveIndex != b.Progression.CurrentObjectiveIndex) throw new Exception("Objective index diverged");
        if (a.Buildings.Count != b.Buildings.Count) throw new Exception("Building count diverged");
        if (a.Buildings[1].Level != b.Buildings[1].Level) throw new Exception("Building level diverged");
    }

    private static GameState Replay(IEnumerable<IGameCommand> commands)
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var state = GameState.CreateInitial(objectives);
        foreach (var command in commands)
        {
            state = DeterministicSimulator.Apply(state, command).State;
        }

        return state;
    }
}
