using System.Text.Json;
using LandBuilder.Domain;

namespace LandBuilder.Infrastructure;

public sealed class SaveRepository
{
    private const int CurrentSchemaVersion = 1;

    public void Save(string path, GameState state)
    {
        var payload = new SavePayload
        {
            SchemaVersion = CurrentSchemaVersion,
            Coins = state.Economy.Coins,
            Tiles = state.World.Tiles.Values
                .Select(t => new SaveTile
                {
                    TileId = t.TileId,
                    Ownership = (int)t.Ownership,
                    UnlockCost = t.UnlockCost,
                    AdjacentTileIds = t.AdjacentTileIds
                }).ToList()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public GameState Load(string path)
    {
        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<SavePayload>(json)
                      ?? throw new InvalidOperationException("Save file invalid");

        if (payload.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported schema version: {payload.SchemaVersion}");
        }

        var tiles = payload.Tiles.ToDictionary(
            t => t.TileId,
            t => new TileState(t.TileId, (TileOwnership)t.Ownership, t.UnlockCost, t.AdjacentTileIds));

        return new GameState(new WorldState(tiles), new EconomyState(payload.Coins), new MetaState(payload.SchemaVersion));
    }

    private sealed class SavePayload
    {
        public int SchemaVersion { get; set; }
        public int Coins { get; set; }
        public List<SaveTile> Tiles { get; set; } = new();
    }

    private sealed class SaveTile
    {
        public int TileId { get; set; }
        public int Ownership { get; set; }
        public int UnlockCost { get; set; }
        public int[] AdjacentTileIds { get; set; } = Array.Empty<int>();
    }
}
