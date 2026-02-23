using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using Xunit;

namespace LandBuilder.Tests;

public class LandBuilderUnlockProgressionTests
{
    [Fact]
    public void InitialState_HasOnlyPlainsUnlocked()
    {
        var state = GameState.CreateInitial(123);

        Assert.Single(state.UnlockedTiles);
        Assert.Contains(TileType.Plains, state.UnlockedTiles);
    }

    [Fact]
    public void UnlockCosts_HasExplicitEntryForEveryTileType()
    {
        var allTiles = Enum.GetValues<TileType>();

        Assert.Equal(allTiles.Length, GameState.UnlockCosts.Count);
        foreach (var tile in allTiles)
            Assert.True(GameState.UnlockCosts.ContainsKey(tile));
    }

    [Fact]
    public void Unlock_DeductsCoinsCorrectly()
    {
        var state = GameState.CreateInitial(1);
        state.Coins = 25;

        var next = state.Unlock(TileType.Woods);

        Assert.NotSame(state, next);
        Assert.Equal(25, state.Coins);
        Assert.False(state.IsUnlocked(TileType.Woods));
        Assert.Equal(15, next.Coins);
        Assert.True(next.IsUnlocked(TileType.Woods));
    }

    [Fact]
    public void UnlockingTwice_IsIdempotent()
    {
        var state = GameState.CreateInitial(1);
        state.Coins = 100;

        var unlocked = state.Unlock(TileType.Woods);
        var again = unlocked.Unlock(TileType.Woods);

        Assert.Same(unlocked, again);
        Assert.Equal(unlocked.Coins, again.Coins);
    }

    [Fact]
    public void Unlock_WithoutEnoughCoins_Throws()
    {
        var state = GameState.CreateInitial(1);
        state.Coins = 9;

        Assert.Throws<InvalidOperationException>(() => state.Unlock(TileType.Woods));
    }

    [Fact]
    public void Draw_NeverProducesLockedTile()
    {
        var sim = new DeterministicSimulator();
        var state = GameState.CreateInitial(98765);

        for (var i = 0; i < 20; i++)
        {
            (state, _) = sim.Apply(state, new DrawTileCommand());
            Assert.NotNull(state.CurrentTile);
            Assert.True(state.IsUnlocked(state.CurrentTile!.Value));
            Assert.Equal(TileType.Plains, state.CurrentTile);
        }
    }

    [Fact]
    public void SaveLoad_PreservesUnlockedTiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"landbuilder-stage14-unlocks-{Guid.NewGuid():N}.json");
        try
        {
            var repo = new SaveRepository();
            var state = GameState.CreateInitial(42);
            state.Coins = 100;
            state = state.Unlock(TileType.Woods);
            state = state.Unlock(TileType.River);

            repo.Save(path, state);
            Assert.True(repo.TryLoad(path, out var loaded, out var error), error);

            Assert.Equal(state, loaded);
            Assert.Equal(state.GetHashCode(), loaded.GetHashCode());
            Assert.Equal(state.UnlockedTiles.OrderBy(x => x), loaded.UnlockedTiles.OrderBy(x => x));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void Stage13Save_LoadsWithAllTilesUnlocked()
    {
        var path = Path.Combine(Path.GetTempPath(), $"landbuilder-stage13-compat-{Guid.NewGuid():N}.json");
        try
        {
            var repo = new SaveRepository();
            repo.Save(path, GameState.CreateInitial(7));

            var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new InvalidDataException("Expected JSON object payload.");

            var schemaKey = root.ContainsKey("SchemaVersion") ? "SchemaVersion" : "schemaVersion";
            root[schemaKey] = 1;

            var unlockedKey = root.FirstOrDefault(kvp =>
                kvp.Key.Contains("unlocked", StringComparison.OrdinalIgnoreCase)).Key;
            if (!string.IsNullOrWhiteSpace(unlockedKey))
                root.Remove(unlockedKey);

            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            Assert.True(repo.TryLoad(path, out var loaded, out var error), error);

            Assert.Equal(GameState.AllTileTypes.Count, loaded.UnlockedTiles.Count);
            foreach (var tile in GameState.AllTileTypes)
                Assert.True(loaded.IsUnlocked(tile));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void EqualityAndHash_IncludeUnlockedTiles()
    {
        var a = GameState.CreateInitial(11);
        a.Coins = 100;

        var b = a.Unlock(TileType.Woods);
        var c = a.Clone();

        Assert.NotEqual(a, b);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(a, c);
        Assert.Equal(a.GetHashCode(), c.GetHashCode());
    }

    [Fact]
    public void ReplayDeterminism_IsPreservedAfterUnlock()
    {
        var seed = 140001UL;
        var preSaveCommands = new IGameCommand[]
        {
            new UnlockTileCommand(TileType.Woods),
            new DrawTileCommand(),
            new UnlockTileCommand(TileType.River)
        };
        var postSaveCommands = new IGameCommand[]
        {
            new DrawTileCommand(),
            new DrawTileCommand(),
            new DrawTileCommand()
        };

        var controlState = GameState.CreateInitial(seed);
        controlState.Coins = 100;
        var replayState = GameState.CreateInitial(seed);
        replayState.Coins = 100;

        var control = new GameSession(controlState, new InMemoryEventSink());
        foreach (var c in preSaveCommands) control.IssueCommand(c);
        foreach (var c in postSaveCommands) control.IssueCommand(c);

        var replay = new GameSession(replayState, new InMemoryEventSink());
        foreach (var c in preSaveCommands) replay.IssueCommand(c);

        var path = Path.Combine(Path.GetTempPath(), $"landbuilder-stage14-replay-{Guid.NewGuid():N}.json");
        try
        {
            var repo = new SaveRepository();
            repo.Save(path, replay.State);
            Assert.True(repo.TryLoad(path, out var loaded, out var error), error);

            replay = new GameSession(loaded, new InMemoryEventSink());
            foreach (var c in postSaveCommands) replay.IssueCommand(c);

            Assert.Equal(control.State, replay.State);
            Assert.Equal(control.State.GetHashCode(), replay.State.GetHashCode());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }
}
