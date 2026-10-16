namespace LandBuilder.Domain;

/// <summary>
/// Terrain carried by each of a tile's six sectors. Two touching edges "match" when they share a terrain.
/// The numeric values are persisted in saves; append new terrains, never reorder.
/// </summary>
public enum Terrain
{
    Grass = 0,
    Forest = 1,
    Field = 2,
    Village = 3,
    Water = 4,
    Mountain = 5,
    Sand = 6
}

public sealed record TerrainInfo(Terrain Terrain, string DisplayName, string QuestNoun, int QuestWeight);

public static class Terrains
{
    public static IReadOnlyList<Terrain> All { get; } = Enum.GetValues<Terrain>();

    /// <summary>Terrains available from the first tile of every run.</summary>
    public static IReadOnlyList<Terrain> Starting { get; } =
        [Terrain.Grass, Terrain.Forest, Terrain.Field, Terrain.Village, Terrain.Water];

    private static readonly Dictionary<Terrain, TerrainInfo> Infos = new()
    {
        [Terrain.Grass] = new(Terrain.Grass, "Meadow", "meadow", 0),
        [Terrain.Forest] = new(Terrain.Forest, "Forest", "forest", 5),
        [Terrain.Field] = new(Terrain.Field, "Wheat Field", "fields", 4),
        [Terrain.Village] = new(Terrain.Village, "Village", "village", 4),
        [Terrain.Water] = new(Terrain.Water, "Lake", "lake", 3),
        [Terrain.Mountain] = new(Terrain.Mountain, "Mountains", "range", 3),
        [Terrain.Sand] = new(Terrain.Sand, "Dunes", "dunes", 2)
    };

    public static TerrainInfo Info(Terrain terrain) => Infos[terrain];

    public static string DisplayName(Terrain terrain) => Infos[terrain].DisplayName;

    /// <summary>Grass is the neutral filler terrain and never gets quests.</summary>
    public static bool CanHostQuest(Terrain terrain) => Infos[terrain].QuestWeight > 0;
}
