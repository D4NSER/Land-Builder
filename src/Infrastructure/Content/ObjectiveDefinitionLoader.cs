using System.Text.Json;
using LandBuilder.Domain;

namespace LandBuilder.Infrastructure.Content;

public sealed class ObjectiveDefinitionLoader
{
    public IReadOnlyList<ObjectiveDefinition> Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Objective file not found: {path}");

        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<ObjectiveFilePayload>(json)
                      ?? throw new InvalidOperationException("Objective file is invalid JSON");

        if (payload.Objectives is null || payload.Objectives.Count != 6)
            throw new InvalidOperationException("Objective file must contain exactly 6 objectives for MVP-2");

        var ids = new HashSet<string>();
        var result = new List<ObjectiveDefinition>();

        foreach (var row in payload.Objectives)
        {
            if (string.IsNullOrWhiteSpace(row.ObjectiveId))
                throw new InvalidOperationException("ObjectiveId is required");
            if (!ids.Add(row.ObjectiveId))
                throw new InvalidOperationException($"Duplicate objective id: {row.ObjectiveId}");
            if (!Enum.TryParse<ObjectiveType>(row.Type, out var objectiveType))
                throw new InvalidOperationException($"Invalid objective type: {row.Type}");
            if (row.TargetValue <= 0)
                throw new InvalidOperationException($"Objective target must be > 0: {row.ObjectiveId}");

            if (objectiveType is ObjectiveType.PlaceBuildingTypeCount or ObjectiveType.UpgradeBuildingToLevelAtLeast or ObjectiveType.PlaceBuildingTypeOnTile)
            {
                if (string.IsNullOrWhiteSpace(row.BuildingTypeId))
                    throw new InvalidOperationException($"BuildingTypeId is required: {row.ObjectiveId}");
            }

            if (objectiveType == ObjectiveType.PlaceBuildingTypeOnTile && row.TileId is null)
                throw new InvalidOperationException($"TileId is required for PlaceBuildingTypeOnTile: {row.ObjectiveId}");

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
