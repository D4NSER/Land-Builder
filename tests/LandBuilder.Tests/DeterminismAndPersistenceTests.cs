using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using Xunit;

namespace LandBuilder.Tests;

public sealed class DeterminismAndPersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "landbuilder-tests-" + Guid.NewGuid().ToString("N"));

    public DeterminismAndPersistenceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private static GameState PlayGreedy(GameState state, int moves)
    {
        for (var i = 0; i < moves && !state.IsGameOver; i++)
        {
            var best = GameEngine.FindBestPlacement(state)!;
            (state, _) = GameEngine.Place(state, best.Slot, best.Rotation);
        }

        return state;
    }

    [Fact]
    public void SameSeedAndMoves_ProduceIdenticalRuns()
    {
        var a = PlayGreedy(GameState.CreateInitial(1234), 40);
        var b = PlayGreedy(GameState.CreateInitial(1234), 40);

        Assert.Equal(a.Fingerprint(), b.Fingerprint());
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentHands()
    {
        var seen = Enumerable.Range(1, 20)
            .Select(s => GameState.CreateInitial((ulong)s).CurrentTile)
            .Distinct()
            .Count();

        Assert.True(seen > 5);
    }

    [Fact]
    public void SaveLoad_RoundTripsFullState_AndReplayContinuesIdentically()
    {
        var path = Path.Combine(_dir, "save.json");
        var repo = new SaveRepository();
        var mid = PlayGreedy(GameState.CreateInitial(77, WorldTheme.Nordic), 45);

        Assert.True(mid.Unlocked.Count > Terrains.Starting.Count, "the run should have unlocked something to persist");
        repo.Save(path, mid);
        Assert.True(repo.TryLoad(path, out var loaded, out var error), error);
        Assert.Equal(mid.Fingerprint(), loaded.Fingerprint());

        Assert.Equal(PlayGreedy(mid, 20).Fingerprint(), PlayGreedy(loaded, 20).Fingerprint());
    }

    [Fact]
    public void Load_RejectsCorruptAndLegacySaves()
    {
        var repo = new SaveRepository();
        var corrupt = Path.Combine(_dir, "corrupt.json");
        File.WriteAllText(corrupt, "{ not json");
        Assert.False(repo.TryLoad(corrupt, out _, out var corruptError));
        Assert.Contains("corrupt", corruptError);

        var legacy = Path.Combine(_dir, "legacy.json");
        File.WriteAllText(legacy, "{\"SchemaVersion\":4,\"Coins\":10}");
        Assert.False(repo.TryLoad(legacy, out _, out var legacyError));
        Assert.Contains("Unsupported", legacyError);

        Assert.Throws<InvalidDataException>(() => repo.Load(legacy));
        Assert.False(repo.TryLoad(Path.Combine(_dir, "missing.json"), out _, out _));
    }

    [Fact]
    public void HighScores_KeepBestAndRankedLeaderboard()
    {
        var path = Path.Combine(_dir, "scores.json");
        var repo = new HighScoreRepository();

        Assert.Equal(0, repo.LoadHighScore(path));
        Assert.Equal(1, repo.RecordRun(path, new RunRecord(500, 40, 2, 5, DateTime.UtcNow)));
        Assert.Equal(1, repo.RecordRun(path, new RunRecord(900, 60, 4, 7, DateTime.UtcNow)));
        Assert.Equal(3, repo.RecordRun(path, new RunRecord(100, 20, 0, 1, DateTime.UtcNow)));

        Assert.Equal(900, repo.LoadHighScore(path));
        Assert.True(repo.TryLoadBoard(path, out var board, out _));
        Assert.Equal([900L, 500L, 100L], board.Select(r => r.Score));

        File.WriteAllText(path, "garbage");
        Assert.Throws<InvalidDataException>(() => repo.LoadHighScore(path));
    }

    [Fact]
    public void HighScores_ReadLegacySchemaOne()
    {
        var path = Path.Combine(_dir, "legacy-scores.json");
        File.WriteAllText(path, "{\"SchemaVersion\":1,\"HighScore\":41}");

        Assert.Equal(41, new HighScoreRepository().LoadHighScore(path));
    }
}
