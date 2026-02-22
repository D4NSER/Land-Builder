using System.Text.Json;
using LandBuilder.Domain;

namespace LandBuilder.Infrastructure;

public sealed class SaveRepository
{
    private const int CurrentSchemaVersion = 2;

    public void Save(string path, GameState state)
    {
        var payload = new SavePayload
        {
            SchemaVersion = CurrentSchemaVersion,
            Coins = state.Economy.Coins,
            NextBuildingId = state.NextBuildingId,
            Tiles = state.World.Tiles.Values
                .Select(t => new SaveTile
                {
                    TileId = t.TileId,
                    Ownership = (int)t.Ownership,
                    UnlockCost = t.UnlockCost,
                    AdjacentTileIds = t.AdjacentTileIds,
                    MaxBuildingSlots = t.MaxBuildingSlots
                }).ToList(),
            Buildings = state.Buildings.Values
                .Select(b => new SaveBuilding
                {
                    BuildingId = b.BuildingId,
                    BuildingTypeId = b.BuildingTypeId,
                    TileId = b.TileId,
                    Level = b.Level
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

        if (payload.SchemaVersion is not (1 or CurrentSchemaVersion))
        {
            throw new InvalidOperationException($"Unsupported schema version: {payload.SchemaVersion}");
        }

        var tiles = payload.Tiles.ToDictionary(
            t => t.TileId,
            t => new TileState(t.TileId, (TileOwnership)t.Ownership, t.UnlockCost, t.AdjacentTileIds, t.MaxBuildingSlots));

        if (payload.SchemaVersion == 1)
        {
            // Migration stub v1 -> v2: initialize building data defaults.
            return new GameState(
                new WorldState(tiles),
                new EconomyState(payload.Coins),
                new MetaState(CurrentSchemaVersion),
                new Dictionary<int, BuildingState>(),
                1);
        }

        var buildings = payload.Buildings.ToDictionary(
            b => b.BuildingId,
            b => new BuildingState(b.BuildingId, b.BuildingTypeId, b.TileId, b.Level));

        return new GameState(
            new WorldState(tiles),
            new EconomyState(payload.Coins),
            new MetaState(CurrentSchemaVersion),
            buildings,
            payload.NextBuildingId <= 0 ? 1 : payload.NextBuildingId);
    }

    private sealed class SavePayload
    {
        public int SchemaVersion { get; set; }
        public int Coins { get; set; }
        public int NextBuildingId { get; set; }
        public List<SaveTile> Tiles { get; set; } = new();
        public List<SaveBuilding> Buildings { get; set; } = new();
    }

    private sealed class SaveTile
    {
        public int TileId { get; set; }
        public int Ownership { get; set; }
        public int UnlockCost { get; set; }
        public int[] AdjacentTileIds { get; set; } = Array.Empty<int>();
        public int MaxBuildingSlots { get; set; } = 1;
    }

    private sealed class SaveBuilding
    {
        public int BuildingId { get; set; }
        public string BuildingTypeId { get; set; } = string.Empty;
        public int TileId { get; set; }
        public int Level { get; set; }
    }
}
