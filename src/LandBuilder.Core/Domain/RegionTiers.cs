namespace LandBuilder.Domain;

/// <summary>One rung of a region's growth ladder, e.g. a village region growing into a Town.</summary>
public sealed record RegionTier(Terrain Terrain, int Level, string Name, int MinSize, int Points, int Tiles);

/// <summary>
/// Connected regions level up as they grow: a cluster of village wedges becomes a Village, then a Town, a City and
/// finally a Capital. Each new level pays points and tiles, so joining land is always worth more than scattering it,
/// and the presentation builds bigger settlements, woods and lakes to match. Sizes count sectors (six per tile).
/// </summary>
public static class RegionTiers
{
    public const int MaxLevel = 5;

    private static readonly int[] Sizes = [8, 16, 30, 48, 72];
    private static readonly int[] Points = [40, 90, 160, 260, 400];
    private static readonly int[] Tiles = [0, 1, 1, 2, 2];

    private static readonly Dictionary<Terrain, string[]> Names = new()
    {
        [Terrain.Village] = ["Village", "Town", "City", "Metropolis", "Capital"],
        [Terrain.Forest] = ["Grove", "Woods", "Forest", "Great Forest", "Wildwood"],
        [Terrain.Field] = ["Farm", "Farmland", "Granary", "Breadbasket", "Golden Plains"],
        [Terrain.Water] = ["Pond", "Lake", "Great Lake", "Inland Sea", "Sea of Isles"],
        [Terrain.Mountain] = ["Hills", "Ridge", "Range", "Massif", "Great Divide"],
        [Terrain.Sand] = ["Dunes", "Desert", "Great Desert", "Sand Sea", "Endless Dunes"],
        [Terrain.Snow] = ["Snowfield", "Tundra", "Glacier", "Ice Sheet", "Frozen Realm"],
        [Terrain.Swamp] = ["Marsh", "Bog", "Wetland", "Everglade", "Great Mire"],
        [Terrain.Orchard] = ["Orchard", "Groves", "Fruit Vale", "Harvest Valley", "Eden"],
        [Terrain.River] = ["Stream", "River", "Great River", "Grand Delta", "Mother River"]
    };

    /// <summary>Meadow is the filler terrain; it never levels up.</summary>
    public static bool HasTiers(Terrain terrain) => Names.ContainsKey(terrain);

    /// <summary>Rivers are long and thin (two sectors per tile), so their ladder is half as tall.</summary>
    private static int SizeFor(Terrain terrain, int index) => terrain == Terrain.River ? Sizes[index] / 2 : Sizes[index];

    /// <summary>0 below the first rung, then 1..<see cref="MaxLevel"/>.</summary>
    public static int Level(Terrain terrain, int size)
    {
        if (!HasTiers(terrain))
            return 0;
        var level = 0;
        for (var i = 0; i < Sizes.Length; i++)
        {
            if (size >= SizeFor(terrain, i))
                level = i + 1;
        }

        return level;
    }

    public static RegionTier Get(Terrain terrain, int level)
    {
        if (!HasTiers(terrain) || level < 1 || level > MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        var i = level - 1;
        return new RegionTier(terrain, level, Names[terrain][i], SizeFor(terrain, i), Points[i], Tiles[i]);
    }

    /// <summary>The next rung above <paramref name="size"/>, or null at the top of the ladder.</summary>
    public static RegionTier? Next(Terrain terrain, int size)
    {
        var level = Level(terrain, size);
        return HasTiers(terrain) && level < MaxLevel ? Get(terrain, level + 1) : null;
    }

    /// <summary>What a small, untiered region is called.</summary>
    public static string BaseName(Terrain terrain) => terrain switch
    {
        Terrain.Village => "Hamlet",
        Terrain.Forest => "Copse",
        Terrain.Field => "Plot",
        Terrain.Water => "Puddle",
        Terrain.Mountain => "Crag",
        Terrain.River => "Brook",
        Terrain.Sand => "Sandpit",
        Terrain.Snow => "Snowdrift",
        Terrain.Swamp => "Mire",
        Terrain.Orchard => "Fruit trees",
        _ => Terrains.Info(terrain).DisplayName
    };

    public static string NameFor(Terrain terrain, int size)
    {
        var level = Level(terrain, size);
        return level == 0 ? BaseName(terrain) : Get(terrain, level).Name;
    }

    /// <summary>Completing (fully enclosing) a region pays by its size; tiny pockets and meadows pay nothing.</summary>
    public static (int Points, int Tiles) ClosureReward(Terrain terrain, int size)
    {
        if (!HasTiers(terrain) || size < GameRules.MinClosedRegion)
            return (0, 0);
        var tiles = size >= 40 ? 2 : size >= 20 ? 1 : 0;
        return (size * GameRules.PointsPerClosedSector, tiles);
    }
}
