namespace LandBuilder.Domain;

/// <summary>
/// Terrain carried by each of a tile's six sectors. Two touching edges "match" when they share a terrain
/// (rivers may also flow into lakes). The numeric values are persisted in saves; append new terrains, never reorder.
/// </summary>
public enum Terrain
{
    Grass = 0,
    Forest = 1,
    Field = 2,
    Village = 3,
    Water = 4,
    Mountain = 5,
    Sand = 6,
    Snow = 7,
    Swamp = 8,
    Orchard = 9,

    /// <summary>A river channel crossing the tile. River edges must meet river (or lake) edges.</summary>
    River = 10
}

public sealed record TerrainInfo(Terrain Terrain, string DisplayName, string QuestNoun, int QuestWeight);

public static class Terrains
{
    public static IReadOnlyList<Terrain> All { get; } = Enum.GetValues<Terrain>();

    public static int Count => All.Count;

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
        [Terrain.Sand] = new(Terrain.Sand, "Dunes", "dunes", 2),
        [Terrain.Snow] = new(Terrain.Snow, "Snowfield", "snowfield", 3),
        [Terrain.Swamp] = new(Terrain.Swamp, "Marsh", "marsh", 3),
        [Terrain.Orchard] = new(Terrain.Orchard, "Orchard", "orchard", 4),
        [Terrain.River] = new(Terrain.River, "River", "river", 3)
    };

    public static TerrainInfo Info(Terrain terrain) => Infos[terrain];

    public static string DisplayName(Terrain terrain) => Infos[terrain].DisplayName;

    /// <summary>Grass is the neutral filler terrain and never gets quests.</summary>
    public static bool CanHostQuest(Terrain terrain) => Infos[terrain].QuestWeight > 0;

    /// <summary>True when two touching edges count as a match.</summary>
    public static bool Matches(Terrain a, Terrain b) =>
        a == b || (a == Terrain.River && b == Terrain.Water) || (a == Terrain.Water && b == Terrain.River);

    /// <summary>River edges may only touch river or lake edges; anything else would leave a dangling channel.</summary>
    public static bool IsLegalContact(Terrain a, Terrain b)
    {
        if (a != Terrain.River && b != Terrain.River)
            return true;
        return Matches(a, b);
    }
}
