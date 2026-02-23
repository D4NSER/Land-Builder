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

public class LandBuilderScoringAndHighScoreTests
{
    [Fact]
    public void Score_EmptyBoard_IsZero()
    {
        var state = GameState.CreateInitial(1);

        Assert.Equal(0, state.Score);
    }

    [Fact]
    public void Score_BasePoints_AreCorrectForEachTileType()
    {
        var expected = new (TileType Tile, int Score)[]
        {
            (TileType.Plains, 1),
            (TileType.Woods, 2),
            (TileType.River, 3),
            (TileType.Meadow, 4),
            (TileType.Village, 5),
            (TileType.Lake, 6)
        };

        foreach (var (tile, score) in expected)
        {
            var state = GameState.CreateInitial(1);
            state.Board[4] = new PlacedTile(tile, 3);
            Assert.Equal(score, state.Score);
        }
    }

    [Fact]
    public void Score_AdjacencyBonus_CountsPairsOnce()
    {
        var state = GameState.CreateInitial(1);
        state.Board[0] = new PlacedTile(TileType.Plains, 0);
        state.Board[1] = new PlacedTile(TileType.Plains, 1);
        state.Board[2] = new PlacedTile(TileType.Plains, 2);

        // Base = 3, same-type adjacent pairs = (0,1) and (1,2) => +4.
        Assert.Equal(7, state.Score);
    }

    [Fact]
    public void Score_FullBoardBonus_AppliesWhenFilled()
    {
        var state = GameState.CreateInitial(1);
        for (var i = 0; i < GameState.BoardWidth * GameState.BoardHeight; i++)
            state.Board[i] = new PlacedTile(TileType.Plains, i % 4);

        // Base 9 + adjacency (12 pairs * 2) + full board bonus 10.
        Assert.Equal(43, state.Score);
    }

    [Fact]
    public void SaveLoad_Schema3_WithScoreField_ValidatesScore()
    {
        var path = TempPath("stage15-save-v3-valid");
        try
        {
            var repo = new SaveRepository();
            var state = BuildScoredState();

            repo.Save(path, state);

            Assert.True(repo.TryLoad(path, out var loaded, out var error), error);
            Assert.Equal(state.Score, loaded.Score);
            Assert.Equal(state, loaded);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void SaveLoad_Schema3_WithWrongScoreField_IsRejected()
    {
        var path = TempPath("stage15-save-v3-invalid-score");
        try
        {
            var repo = new SaveRepository();
            var state = BuildScoredState();
            repo.Save(path, state);

            var root = ParseObject(path);
            var scoreKey = FindKey(root, "score");
            root[scoreKey] = state.Score + 1;
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var ok = repo.TryLoad(path, out _, out var error);

            Assert.False(ok);
            Assert.Contains("score", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Load_Schema2_RecomputesScore()
    {
        var path = TempPath("stage15-save-v2-compat");
        try
        {
            var repo = new SaveRepository();
            var state = BuildScoredState();
            repo.Save(path, state);

            var root = ParseObject(path);
            root[FindKey(root, "schema")] = 2;
            RemoveKeyIfPresent(root, "score");
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            Assert.True(repo.TryLoad(path, out var loaded, out var error), error);
            Assert.Equal(state.Score, loaded.Score);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Load_Schema1_RecomputesScore_AndInfersAllUnlocked()
    {
        var path = TempPath("stage15-save-v1-compat");
        try
        {
            var repo = new SaveRepository();
            var state = BuildScoredState();
            repo.Save(path, state);

            var root = ParseObject(path);
            root[FindKey(root, "schema")] = 1;
            RemoveKeyIfPresent(root, "score");
            RemoveKeyIfPresent(root, "unlocked");
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            Assert.True(repo.TryLoad(path, out var loaded, out var error), error);
            Assert.Equal(state.Score, loaded.Score);
            Assert.Equal(GameState.AllTileTypes.Count, loaded.UnlockedTiles.Count);
            foreach (var tile in GameState.AllTileTypes)
                Assert.True(loaded.IsUnlocked(tile));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void HighScore_MissingFile_DefaultsToZero()
    {
        var repo = new HighScoreRepository();
        var path = TempPath("stage15-highscore-missing");
        Cleanup(path);

        Assert.True(repo.TryLoadHighScore(path, out var score, out var error), error);
        Assert.Equal(0, score);
        Assert.Equal(0, repo.LoadHighScore(path));
    }

    [Fact]
    public void HighScore_SaveLoad_RoundTrip()
    {
        var repo = new HighScoreRepository();
        var path = TempPath("stage15-highscore-roundtrip");
        try
        {
            repo.SaveHighScore(path, 123);

            Assert.True(repo.TryLoadHighScore(path, out var score, out var error), error);
            Assert.Equal(123, score);
            Assert.Equal(123, repo.LoadHighScore(path));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void HighScore_CorruptFile_ThrowsInvalidDataException()
    {
        var repo = new HighScoreRepository();
        var path = TempPath("stage15-highscore-corrupt");
        try
        {
            File.WriteAllText(path, "{ not-json }");
            var ex = Assert.Throws<InvalidDataException>(() => repo.LoadHighScore(path));
            Assert.True(
                ex.Message.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void SubmitScore_UpdatesHighScoreOnlyWhenHigher()
    {
        var sink = new InMemoryEventSink();
        var highScoreRepo = new HighScoreRepository();
        var path = TempPath("stage15-submit-highscore");
        try
        {
            var highScoreState = BuildScoredState();
            var session = new GameSession(highScoreState, sink, highScoreRepo, path);
            session.IssueCommand(new SubmitScoreCommand());

            Assert.Equal(highScoreState.Score, highScoreRepo.LoadHighScore(path));
            Assert.Single(sink.Events.OfType<HighScoreUpdatedEvent>());

            var lowerState = GameState.CreateInitial(1);
            lowerState.Board[0] = new PlacedTile(TileType.Plains, 0); // score = 1
            var lowerSession = new GameSession(lowerState, sink, highScoreRepo, path);
            lowerSession.IssueCommand(new SubmitScoreCommand());

            Assert.Equal(highScoreState.Score, highScoreRepo.LoadHighScore(path));
            Assert.Single(sink.Events.OfType<HighScoreUpdatedEvent>());
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void ReplayDeterminism_PreservesScore()
    {
        var seed = 150015UL;
        var preSaveCommands = new IGameCommand[]
        {
            new DrawTileCommand(),
            new PlaceTileCommand(0),
            new DrawTileCommand(),
            new PlaceTileCommand(1)
        };
        var postSaveCommands = new IGameCommand[]
        {
            new DrawTileCommand(),
            new PlaceTileCommand(3),
            new DrawTileCommand(),
            new PlaceTileCommand(4)
        };

        var control = new GameSession(GameState.CreateInitial(seed), new InMemoryEventSink());
        foreach (var c in preSaveCommands) control.IssueCommand(c);
        foreach (var c in postSaveCommands) control.IssueCommand(c);

        var replay = new GameSession(GameState.CreateInitial(seed), new InMemoryEventSink());
        foreach (var c in preSaveCommands) replay.IssueCommand(c);

        var path = TempPath("stage15-replay");
        try
        {
            var saveRepo = new SaveRepository();
            saveRepo.Save(path, replay.State);
            Assert.True(saveRepo.TryLoad(path, out var loaded, out var error), error);

            replay = new GameSession(loaded, new InMemoryEventSink());
            foreach (var c in postSaveCommands) replay.IssueCommand(c);

            Assert.Equal(control.State, replay.State);
            Assert.Equal(control.State.Score, replay.State.Score);
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static GameState BuildScoredState()
    {
        var state = GameState.CreateInitial(10);
        state.Coins = 100;
        state.SetUnlockedTiles(GameState.AllTileTypes);
        state.Board[0] = new PlacedTile(TileType.Plains, 0);
        state.Board[1] = new PlacedTile(TileType.Plains, 1); // +2 adjacency with slot 0
        state.Board[4] = new PlacedTile(TileType.Village, 0);
        state.Board[8] = new PlacedTile(TileType.Lake, 2);
        return state;
    }

    private static string TempPath(string name) => Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.json");

    private static JsonObject ParseObject(string path) =>
        JsonNode.Parse(File.ReadAllText(path))?.AsObject()
        ?? throw new InvalidDataException("Expected JSON object payload.");

    private static string FindKey(JsonObject root, string contains) =>
        root.FirstOrDefault(kvp => kvp.Key.Contains(contains, StringComparison.OrdinalIgnoreCase)).Key
        ?? throw new InvalidDataException($"Missing key containing '{contains}'.");

    private static void RemoveKeyIfPresent(JsonObject root, string contains)
    {
        var key = root.FirstOrDefault(kvp => kvp.Key.Contains(contains, StringComparison.OrdinalIgnoreCase)).Key;
        if (!string.IsNullOrWhiteSpace(key))
            root.Remove(key);
    }

    private static void Cleanup(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
    }
}
