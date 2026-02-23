using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp0DeterminismTests
{
    [Fact]
    public void SameInputsProduceSameOutputs()
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

        Assert.Equal(b.Economy.Coins, a.Economy.Coins);
        Assert.Equal(b.Progression.CurrentObjectiveIndex, a.Progression.CurrentObjectiveIndex);
        Assert.Equal(b.Buildings.Count, a.Buildings.Count);
        Assert.Equal(b.Buildings[1].Level, a.Buildings[1].Level);
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
