using System.Text.Json;
using LandBuilder.Domain;

namespace LandBuilder.Infrastructure;

public sealed class SaveRepository
{
    private const int CurrentSchemaVersion = 3;
    private readonly IReadOnlyList<ObjectiveDefinition> _objectiveDefinitions;

    public SaveRepository(IReadOnlyList<ObjectiveDefinition> objectiveDefinitions)
    {
        _objectiveDefinitions = objectiveDefinitions;
    }

    public void Save(string path, GameState state, string? backupPath = null)
    {
        var payload = new SavePayload
        {
            SchemaVersion = CurrentSchemaVersion,
            Coins = state.Economy.Coins,
            LifetimeCoinsEarned = state.Economy.LifetimeCoinsEarned,
            NextBuildingId = state.NextBuildingId,
            CurrentObjectiveIndex = state.Progression.CurrentObjectiveIndex,
            CompletedObjectiveIds = state.Progression.CompletedObjectiveIds.ToList(),
            UnlockFlags = state.Progression.UnlockFlags.OrderBy(x => x).ToList(),
            LastCompletedObjectiveId = state.Progression.LastCompletedObjectiveId,
            LastCompletionMessage = state.Progression.LastCompletionMessage,
            Tiles = state.World.Tiles.Values.Select(t => new SaveTile
            {
                TileId = t.TileId,
                Ownership = (int)t.Ownership,
                UnlockCost = t.UnlockCost,
                AdjacentTileIds = t.AdjacentTileIds,
                MaxBuildingSlots = t.MaxBuildingSlots,
                Terrain = (int)t.Terrain,
                RegionDepth = t.RegionDepth
            }).ToList(),
            Buildings = state.Buildings.Values.Select(b => new SaveBuilding
            {
                BuildingId = b.BuildingId,
                BuildingTypeId = b.BuildingTypeId,
                TileId = b.TileId,
                Level = b.Level
            }).ToList()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        try
        {
            WriteAtomic(path, json);
            if (!string.IsNullOrWhiteSpace(backupPath))
            {
                WriteAtomic(backupPath!, json);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Save failed. Verify write permissions for '{path}'.", ex);
        }
    }

    public GameState Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var payload = JsonSerializer.Deserialize<SavePayload>(json)
                          ?? throw new InvalidOperationException("Save file is invalid JSON payload.");

            return payload.SchemaVersion switch
            {
                2 => MigrateV2ToV3(payload),
                CurrentSchemaVersion => BuildV3(payload),
                _ => throw new InvalidOperationException($"Unsupported save schema version: {payload.SchemaVersion}. Expected 2 or {CurrentSchemaVersion}.")
            };
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to load save '{path}'. File may be missing or corrupted.", ex);
        }
    }

    public (GameState State, string StatusMessage) LoadWithRecovery(string primaryPath, string backupPath, Func<GameState> safeDefaultFactory)
    {
        try
        {
            return (Load(primaryPath), "Loaded primary save.");
        }
        catch (Exception primaryEx)
        {
            Console.Error.WriteLine(primaryEx.Message);
            try
            {
                return (Load(backupPath), "Primary save was invalid. Loaded backup save.");
            }
            catch (Exception backupEx)
            {
                Console.Error.WriteLine(backupEx.Message);
                return (safeDefaultFactory(), "Primary and backup saves were unavailable. Started a safe default session.");
            }
        }
    }

    private static void WriteAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, contents);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }

    private GameState MigrateV2ToV3(SavePayload payload)
    {
        var migrated = new SavePayload
        {
            SchemaVersion = CurrentSchemaVersion,
            Coins = payload.Coins,
            LifetimeCoinsEarned = payload.Coins,
            NextBuildingId = payload.NextBuildingId,
            CurrentObjectiveIndex = 0,
            CompletedObjectiveIds = new List<string>(),
            UnlockFlags = new List<string>(),
            LastCompletedObjectiveId = string.Empty,
            LastCompletionMessage = string.Empty,
            Tiles = payload.Tiles,
            Buildings = payload.Buildings
        };

        return BuildV3(migrated);
    }

    private GameState BuildV3(SavePayload payload)
    {
        var tiles = payload.Tiles.ToDictionary(
            t => t.TileId,
            t => new TileState(t.TileId, (TileOwnership)t.Ownership, t.UnlockCost, t.AdjacentTileIds, t.MaxBuildingSlots, (TerrainType)t.Terrain, t.RegionDepth));

        var buildings = payload.Buildings.ToDictionary(
            b => b.BuildingId,
            b => new BuildingState(b.BuildingId, b.BuildingTypeId, b.TileId, b.Level));

        var progression = new ProgressionState(
            payload.CurrentObjectiveIndex,
            payload.CompletedObjectiveIds,
            payload.UnlockFlags.ToHashSet(),
            payload.LastCompletedObjectiveId,
            payload.LastCompletionMessage);

        return new GameState(
            new WorldState(tiles),
            new EconomyState(payload.Coins, payload.LifetimeCoinsEarned),
            new MetaState(CurrentSchemaVersion),
            buildings,
            payload.NextBuildingId <= 0 ? 1 : payload.NextBuildingId,
            progression,
            _objectiveDefinitions);
    }

    private sealed class SavePayload
    {
        public int SchemaVersion { get; set; }
        public int Coins { get; set; }
        public int LifetimeCoinsEarned { get; set; }
        public int NextBuildingId { get; set; }
        public int CurrentObjectiveIndex { get; set; }
        public List<string> CompletedObjectiveIds { get; set; } = new();
        public List<string> UnlockFlags { get; set; } = new();
        public string LastCompletedObjectiveId { get; set; } = string.Empty;
        public string LastCompletionMessage { get; set; } = string.Empty;
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
        public int Terrain { get; set; }
        public int RegionDepth { get; set; }
    }

    private sealed class SaveBuilding
    {
        public int BuildingId { get; set; }
        public string BuildingTypeId { get; set; } = string.Empty;
        public int TileId { get; set; }
        public int Level { get; set; }
    }
}
