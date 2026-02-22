using LandBuilder.Domain;

namespace LandBuilder.Tests;

public static class Mvp0DeterminismTests
{
    public static void SameInputsProduceSameOutputs()
    {
        var commands = new IGameCommand[]
        {
            new TickCommand(1),
            new ExpandTileCommand(1),
            new TickCommand(3)
        };

        var a = Replay(commands);
        var b = Replay(commands);

        if (a.Economy.Coins != b.Economy.Coins) throw new Exception("Coins diverged");
        if (a.World.Tiles[1].Ownership != b.World.Tiles[1].Ownership) throw new Exception("Tile state diverged");
    }

    private static GameState Replay(IEnumerable<IGameCommand> commands)
    {
        var state = GameState.CreateMvp0Default();
        foreach (var command in commands)
        {
            state = DeterministicSimulator.Apply(state, command).State;
        }

        return state;
    }
}
