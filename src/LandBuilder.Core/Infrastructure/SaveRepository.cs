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

    public GameState Load(string path)
    {
        if (!File.Exists(path))
            return GameState.CreateInitial();

        try
        {
            var payload = JsonSerializer.Deserialize<SavePayload>(File.ReadAllText(path), JsonOptions);
            if (payload is null)
                return GameState.CreateInitial();

            if (payload.SchemaVersion != CurrentSchemaVersion)
                return GameState.CreateInitial();

            return new GameState
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
        }
        catch
        {
            return GameState.CreateInitial();
        }
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
