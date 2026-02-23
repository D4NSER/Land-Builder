using System.Text.Json;
using System.Text.RegularExpressions;
using LandBuilder.Domain;

namespace LandBuilder.Infrastructure.Content;

public sealed class ObjectiveDefinitionLoader
{
    private static readonly Regex ObjectiveSequenceRegex = new("^OBJ_(\\d{2})_", RegexOptions.Compiled);

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

        if (payload.Objectives is null || payload.Objectives.Count != 14)
            throw new InvalidOperationException("Objective file must contain exactly 14 objectives (6 in chapter 1 + 8 in chapter 2) for Stage 8C runtime.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ObjectiveDefinition>();

        for (var i = 0; i < payload.Objectives.Count; i++)
        {
            var row = payload.Objectives[i];

            if (string.IsNullOrWhiteSpace(row.ObjectiveId))
                throw new InvalidOperationException($"ObjectiveId is required and cannot be empty at index {i}.");
            if (!ids.Add(row.ObjectiveId))
                throw new InvalidOperationException($"Duplicate objective id detected: {row.ObjectiveId}");
            if (!Enum.TryParse<ObjectiveType>(row.Type, out var objectiveType))
                throw new InvalidOperationException($"Invalid objective type '{row.Type}' for objective '{row.ObjectiveId}'.");
            if (row.TargetValue <= 0)
                throw new InvalidOperationException($"Objective target must be > 0: {row.ObjectiveId}");

            ValidateSequence(row.ObjectiveId, i + 1);
            ValidatePredicateFields(row, objectiveType);

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

    private static void ValidateSequence(string objectiveId, int expectedIndex)
    {
        var match = ObjectiveSequenceRegex.Match(objectiveId);
        if (!match.Success)
            throw new InvalidOperationException($"Objective id '{objectiveId}' must start with OBJ_XX_ sequence prefix.");

        if (!int.TryParse(match.Groups[1].Value, out var parsedIndex) || parsedIndex != expectedIndex)
            throw new InvalidOperationException($"Objective sequence is out-of-order at '{objectiveId}'. Expected OBJ_{expectedIndex:00}_ prefix.");
    }

    private static void ValidatePredicateFields(ObjectiveRow row, ObjectiveType objectiveType)
    {
        if (objectiveType is ObjectiveType.PlaceBuildingTypeCount or ObjectiveType.UpgradeBuildingToLevelAtLeast or ObjectiveType.PlaceBuildingTypeOnTile)
        {
            if (string.IsNullOrWhiteSpace(row.BuildingTypeId))
                throw new InvalidOperationException($"BuildingTypeId is required for objective: {row.ObjectiveId}");
        }

        if (objectiveType == ObjectiveType.PlaceBuildingTypeOnTile && row.TileId is null)
            throw new InvalidOperationException($"TileId is required for PlaceBuildingTypeOnTile objective: {row.ObjectiveId}");

        if (objectiveType == ObjectiveType.PlaceBuildingTypeCount &&
            string.Equals(row.BuildingTypeId, "MIXED_ALL", StringComparison.OrdinalIgnoreCase) &&
            row.TargetValue != 3)
        {
            throw new InvalidOperationException($"Objective '{row.ObjectiveId}' uses MIXED_ALL and must set TargetValue to 3 (one Camp, one Quarry, one Sawmill).");
        }

        if (objectiveType != ObjectiveType.PlaceBuildingTypeOnTile && row.TileId is not null)
            throw new InvalidOperationException($"TileId is only valid for PlaceBuildingTypeOnTile objective: {row.ObjectiveId}");
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
