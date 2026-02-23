using System.Text.Json;

namespace LandBuilder.Infrastructure;

public sealed class HighScoreRepository
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void SaveHighScore(string path, int highScore)
    {
        var payload = new HighScorePayload
        {
            SchemaVersion = CurrentSchemaVersion,
            HighScore = highScore
        };

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(payload, JsonOptions));
        if (File.Exists(path))
            File.Replace(temp, path, path + ".bak", true);
        else
            File.Move(temp, path);
    }

    public bool TryLoadHighScore(string path, out int highScore, out string error)
    {
        highScore = 0;
        error = string.Empty;

        if (!File.Exists(path))
            return true;

        HighScorePayload? payload;
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

        if (payload.SchemaVersion != CurrentSchemaVersion)
        {
            error = $"Unsupported high score schema: {payload.SchemaVersion}.";
            return false;
        }

        if (payload.HighScore < 0)
        {
            error = "High score payload is invalid.";
            return false;
        }

        highScore = payload.HighScore;
        return true;
    }

    public int LoadHighScore(string path)
    {
        if (TryLoadHighScore(path, out var highScore, out var error))
            return highScore;

        throw new InvalidDataException(error);
    }

    private sealed class HighScorePayload
    {
        public int SchemaVersion { get; set; }
        public int HighScore { get; set; }
    }
}
