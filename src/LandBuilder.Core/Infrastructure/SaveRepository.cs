using System.Text.Json;
using LandBuilder.Domain;

namespace LandBuilder.Infrastructure;

public sealed class SaveRepository
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void Save(string path, GameState state)
    {
        var payload = new SavePayload
        {
            SchemaVersion = CurrentSchemaVersion,
            Coins = state.Coins,
            RngState = state.RngState,
            RngStep = state.RngStep,
            CurrentTile = state.CurrentTile,
            LastMessage = state.LastMessage,
            Board = state.Board.Select(x => new SaveTile
            {
                SlotIndex = x.Key,
                TileType = x.Value.TileType,
                RotationQuarterTurns = x.Value.RotationQuarterTurns
            }).ToList()
        };

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(payload, JsonOptions));
        if (File.Exists(path))
            File.Replace(temp, path, path + ".bak", true);
        else
            File.Move(temp, path);
    }

    public bool TryLoad(string path, out GameState state, out string error)
    {
        state = GameState.CreateInitial();
        error = string.Empty;

        if (!File.Exists(path))
        {
            error = "Save file not found.";
            return false;
        }

        SavePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SavePayload>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            error = $"Save is corrupted or unreadable: {ex.Message}";
            return false;
        }

        if (payload is null)
        {
            error = "Save payload is empty.";
            return false;
        }

        if (payload.SchemaVersion != CurrentSchemaVersion)
        {
            error = $"Unsupported save schema: {payload.SchemaVersion}.";
            return false;
        }

        state = new GameState
        {
            Coins = payload.Coins,
            RngState = payload.RngState,
            RngStep = payload.RngStep,
            CurrentTile = payload.CurrentTile,
            LastMessage = payload.LastMessage ?? "Loaded.",
            Board = payload.Board?.ToDictionary(
                x => x.SlotIndex,
                x => new PlacedTile(x.TileType, x.RotationQuarterTurns)) ?? new Dictionary<int, PlacedTile>()
        };

        return true;
    }

    public GameState Load(string path)
    {
        if (TryLoad(path, out var state, out var error))
            return state;

        throw new InvalidDataException(error);
    }

    private sealed class SavePayload
    {
        public int SchemaVersion { get; set; }
        public long Coins { get; set; }
        public ulong RngState { get; set; }
        public int RngStep { get; set; }
        public TileType? CurrentTile { get; set; }
        public string? LastMessage { get; set; }
        public List<SaveTile>? Board { get; set; }
    }

    private sealed class SaveTile
    {
        public int SlotIndex { get; set; }
        public TileType TileType { get; set; }
        public int RotationQuarterTurns { get; set; }
    }
}
