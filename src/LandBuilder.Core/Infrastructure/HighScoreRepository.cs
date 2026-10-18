using System.Text.Json;

namespace LandBuilder.Infrastructure;

public sealed record RunRecord(long Score, int TilesPlaced, int QuestsCompleted, int BestStreak, DateTime FinishedUtc);

/// <summary>Best score plus a short leaderboard of finished runs.</summary>
public sealed class HighScoreRepository
{
    private const int CurrentSchemaVersion = 2;
    public const int LeaderboardSize = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void SaveHighScore(string path, long highScore)
    {
        var existing = TryLoadBoard(path, out var board, out _) ? board : new List<RunRecord>();
        Write(path, Math.Max(highScore, existing.Select(r => r.Score).DefaultIfEmpty(0).Max()), existing);
    }

    /// <summary>Adds a finished run; returns its 1-based rank, or 0 if it did not make the board.</summary>
    public int RecordRun(string path, RunRecord run)
    {
        TryLoadBoard(path, out var board, out _);
        TryLoadHighScore(path, out var best, out _);
        board.Add(run);
        var ordered = board.OrderByDescending(r => r.Score).ThenBy(r => r.FinishedUtc).Take(LeaderboardSize).ToList();
        Write(path, Math.Max(best, run.Score), ordered);
        var rank = ordered.IndexOf(run);
        return rank < 0 ? 0 : rank + 1;
    }

    public bool TryLoadHighScore(string path, out long highScore, out string error)
    {
        highScore = 0;
        if (!TryRead(path, out var payload, out error))
            return false;
        highScore = payload?.HighScore ?? 0;
        return true;
    }

    public bool TryLoadBoard(string path, out List<RunRecord> board, out string error)
    {
        board = new List<RunRecord>();
        if (!TryRead(path, out var payload, out error))
            return false;
        board = payload?.Runs?.ToList() ?? new List<RunRecord>();
        return true;
    }

    public long LoadHighScore(string path)
    {
        if (TryLoadHighScore(path, out var highScore, out var error))
            return highScore;

        throw new InvalidDataException(error);
    }

    private static void Write(string path, long highScore, List<RunRecord> runs)
    {
        var payload = new HighScorePayload
        {
            SchemaVersion = CurrentSchemaVersion,
            HighScore = highScore,
            Runs = runs
        };
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static bool TryRead(string path, out HighScorePayload? payload, out string error)
    {
        payload = null;
        error = string.Empty;
        if (!File.Exists(path))
            return true;

        try
        {
            payload = JsonSerializer.Deserialize<HighScorePayload>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            error = $"High score file is corrupt or unreadable: {ex.Message}";
            return false;
        }

        if (payload is null)
        {
            error = "High score payload is empty.";
            return false;
        }

        if (payload.SchemaVersion is not (1 or CurrentSchemaVersion))
        {
            error = $"Unsupported high score schema: {payload.SchemaVersion}.";
            payload = null;
            return false;
        }

        if (payload.HighScore < 0)
        {
            error = "High score payload is invalid.";
            payload = null;
            return false;
        }

        return true;
    }

    private sealed class HighScorePayload
    {
        public int SchemaVersion { get; set; }
        public long HighScore { get; set; }
        public List<RunRecord>? Runs { get; set; }
    }
}
