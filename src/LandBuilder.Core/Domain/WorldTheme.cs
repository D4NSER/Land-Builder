namespace LandBuilder.Domain;

/// <summary>
/// Each island rolls a theme. It sets the mood visually and, in the rules, nudges which terrains appear and in which
/// order milestones unlock them.
/// </summary>
public enum WorldTheme
{
    Temperate = 0,
    Nordic = 1,
    Tropical = 2,
    Autumn = 3,
    Savanna = 4,
    Japanese = 5
}

public sealed record ThemeInfo(
    WorldTheme Theme,
    string DisplayName,
    string Tagline,
    IReadOnlyDictionary<int, Terrain> Unlocks,
    IReadOnlyDictionary<Terrain, double> WeightMultipliers);

public static class Themes
{
    public static IReadOnlyList<WorldTheme> All { get; } = Enum.GetValues<WorldTheme>();

    private static readonly Dictionary<WorldTheme, ThemeInfo> Infos = new()
    {
        [WorldTheme.Temperate] = new(WorldTheme.Temperate, "Green Isles", "Rolling meadows and quiet villages",
            new Dictionary<int, Terrain> { [0] = Terrain.Mountain, [1] = Terrain.River, [2] = Terrain.Orchard, [4] = Terrain.Sand },
            new Dictionary<Terrain, double>()),
        [WorldTheme.Nordic] = new(WorldTheme.Nordic, "Northern Reach", "Pine forests under a pale sun",
            new Dictionary<int, Terrain> { [0] = Terrain.Mountain, [1] = Terrain.River, [2] = Terrain.Snow, [4] = Terrain.Swamp },
            new Dictionary<Terrain, double> { [Terrain.Forest] = 1.3, [Terrain.Snow] = 1.4, [Terrain.Field] = 0.7, [Terrain.Sand] = 0.3, [Terrain.Orchard] = 0.4 }),
        [WorldTheme.Tropical] = new(WorldTheme.Tropical, "Coral Coast", "Turquoise lagoons and palm groves",
            new Dictionary<int, Terrain> { [0] = Terrain.Sand, [1] = Terrain.River, [2] = Terrain.Swamp, [4] = Terrain.Mountain },
            new Dictionary<Terrain, double> { [Terrain.Water] = 1.3, [Terrain.Sand] = 1.4, [Terrain.Forest] = 1.1, [Terrain.Field] = 0.8, [Terrain.Snow] = 0.0 }),
        [WorldTheme.Autumn] = new(WorldTheme.Autumn, "Amber Vale", "Copper woods and harvest orchards",
            new Dictionary<int, Terrain> { [0] = Terrain.Orchard, [1] = Terrain.River, [2] = Terrain.Mountain, [4] = Terrain.Swamp },
            new Dictionary<Terrain, double> { [Terrain.Forest] = 1.3, [Terrain.Orchard] = 1.3, [Terrain.Field] = 1.1 }),
        [WorldTheme.Savanna] = new(WorldTheme.Savanna, "Sunlit Plains", "Golden grass and lonely acacias",
            new Dictionary<int, Terrain> { [0] = Terrain.Sand, [1] = Terrain.River, [2] = Terrain.Mountain, [4] = Terrain.Orchard },
            new Dictionary<Terrain, double> { [Terrain.Grass] = 1.2, [Terrain.Sand] = 1.3, [Terrain.Field] = 1.1, [Terrain.Forest] = 0.7, [Terrain.Snow] = 0.0 }),
        [WorldTheme.Japanese] = new(WorldTheme.Japanese, "Sakura Shores", "Cherry blossoms, pagodas and rice terraces",
            new Dictionary<int, Terrain> { [0] = Terrain.Mountain, [1] = Terrain.River, [2] = Terrain.Orchard, [4] = Terrain.Swamp },
            new Dictionary<Terrain, double> { [Terrain.Orchard] = 1.4, [Terrain.Water] = 1.2, [Terrain.Mountain] = 1.2, [Terrain.Field] = 1.1, [Terrain.Sand] = 0.3, [Terrain.Snow] = 0.5 })
    };

    public static ThemeInfo Info(WorldTheme theme) => Infos[theme];

    public static WorldTheme FromSeed(ulong seed)
    {
        var rng = new Rng(seed ^ 0x51ED270B27D3A5F1UL);
        return All[rng.NextInt(All.Count)];
    }

    public static double WeightMultiplier(WorldTheme theme, Terrain terrain) =>
        Infos[theme].WeightMultipliers.TryGetValue(terrain, out var m) ? m : 1.0;

    public static Terrain? Unlock(WorldTheme theme, int milestoneIndex) =>
        Infos[theme].Unlocks.TryGetValue(milestoneIndex, out var t) ? t : null;
}
