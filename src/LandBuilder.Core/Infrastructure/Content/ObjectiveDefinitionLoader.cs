using System.Text.Json;
using LandBuilder.Domain;

namespace LandBuilder.Infrastructure.Content;

public sealed class ObjectiveDefinitionLoader
{
    public IReadOnlyList<ObjectiveDefinition> Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Objective file not found at '{path}'. Ensure data/objectives/mvp2_objectives.json is present.");

        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<ObjectiveFilePayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })
                      ?? throw new InvalidOperationException("Objective file parsing failed: payload is null.");

        if (payload.Objectives is null || payload.Objectives.Count != 6)
            throw new InvalidOperationException("Objective file must contain exactly 6 objectives for MVP-2/MVP-3 runtime.");

        var ids = new HashSet<string>();
        var result = new List<ObjectiveDefinition>();

        foreach (var row in payload.Objectives)
        {
            if (string.IsNullOrWhiteSpace(row.ObjectiveId))
                throw new InvalidOperationException("ObjectiveId is required and cannot be empty.");
            if (!ids.Add(row.ObjectiveId))
                throw new InvalidOperationException($"Duplicate objective id detected: {row.ObjectiveId}");
            if (!Enum.TryParse<ObjectiveType>(row.Type, out var objectiveType))
                throw new InvalidOperationException($"Invalid objective type '{row.Type}' for objective '{row.ObjectiveId}'.");
            if (row.TargetValue <= 0)
                throw new InvalidOperationException($"Objective target must be > 0: {row.ObjectiveId}");

            if (objectiveType is ObjectiveType.PlaceBuildingTypeCount or ObjectiveType.UpgradeBuildingToLevelAtLeast or ObjectiveType.PlaceBuildingTypeOnTile)
            {
                if (string.IsNullOrWhiteSpace(row.BuildingTypeId))
                    throw new InvalidOperationException($"BuildingTypeId is required for objective: {row.ObjectiveId}");
            }

            if (objectiveType == ObjectiveType.PlaceBuildingTypeOnTile && row.TileId is null)
                throw new InvalidOperationException($"TileId is required for PlaceBuildingTypeOnTile objective: {row.ObjectiveId}");

            result.Add(new ObjectiveDefinition(
                row.ObjectiveId,
                objectiveType,
                row.TargetValue,
                row.BuildingTypeId,
                row.TileId,
                row.RewardCoins,
                row.RewardUnlockFlag));
        }

        return result;
    }

    private sealed class ObjectiveFilePayload
    {
        public List<ObjectiveRow> Objectives { get; set; } = new();
    }

    private sealed class ObjectiveRow
    {
        public string ObjectiveId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int TargetValue { get; set; }
        public string? BuildingTypeId { get; set; }
        public int? TileId { get; set; }
        public int RewardCoins { get; set; }
        public string? RewardUnlockFlag { get; set; }
    }
}
