using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10aDeterminismReplayTests
{
    [Fact]
    public void DeterminismReplay_K5_MultiPocketUnlocks_ProducesIdenticalEndStateHash()
    {
        var hashes = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            var state = Replay();
            hashes.Add(Hash(state));
        }

        Assert.True(hashes.All(h => h == hashes[0]));
    }

    private static GameState Replay()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(500, 0) };

        var commands = new IGameCommand[]
        {
            new ExpandTileCommand(1),
            new ExpandTileCommand(3),
            new ExpandTileCommand(4),
            new ExpandTileCommand(2),
            new ExpandTileCommand(6),
            new ExpandTileCommand(5),
            new ExpandTileCommand(7),
            new ExpandTileCommand(8)
        };

        foreach (var command in commands)
            state = DeterministicSimulator.Apply(state, command).State;

        return state;
    }

    private static string Hash(GameState state)
    {
        var tiles = state.World.Tiles.Values
            .OrderBy(t => t.TileId)
            .Select(t => $"{t.TileId}:{(int)t.Ownership}:{t.RegionDepth}");
        var payload = string.Join("|", tiles) + $"|coins:{state.Economy.Coins}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
