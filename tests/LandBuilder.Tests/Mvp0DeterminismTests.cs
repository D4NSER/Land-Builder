using System;
using System.Collections.Generic;
using LandBuilder.Domain;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp0DeterminismTests
{
    [Fact]
    public void SameInputsProduceSameOutputs()
    {
        var commands = new IGameCommand[]
        {
            new TickCommand(1),
            new ExpandTileCommand(1),
            new TickCommand(3)
        };

        var a = Replay(commands);
        var b = Replay(commands);

        Assert.Equal(a.Economy.Coins, b.Economy.Coins);
        Assert.Equal(a.World.Tiles[1].Ownership, b.World.Tiles[1].Ownership);
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