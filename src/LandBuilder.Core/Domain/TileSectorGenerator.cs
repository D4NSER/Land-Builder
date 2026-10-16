namespace LandBuilder.Domain;

public static class TileSectorGenerator
{
    public enum ThemeContext
    {
        None = 0,
        Temperate = 1,
        Tropical = 2,
        Frontier = 3
    }

    private static readonly (int Small, int Large, int Weight)[] TwoTerrainPatterns =
    [
        (1, 5, 30),
        (2, 4, 35),
        (3, 3, 20),
        (2, 4, 10),
        (1, 5, 5)
    ];

    private static readonly HashSet<(TileType, TileType)> ForbiddenPairs =
    [
        (TileType.Volcano, TileType.Pond),
        (TileType.Volcano, TileType.River),
        (TileType.Volcano, TileType.Coral),
        (TileType.Volcano, TileType.Swamp),
        (TileType.Snow, TileType.Desert),
        (TileType.Snow, TileType.Coral),
        (TileType.Snow, TileType.River),
        (TileType.Desert, TileType.Pond),
        (TileType.Desert, TileType.Coral),
        (TileType.Desert, TileType.Woods),
        (TileType.Village, TileType.Volcano),
        (TileType.Village, TileType.Swamp),
        (TileType.Village, TileType.Coral),
        (TileType.Mushroom, TileType.Desert),
        (TileType.Mushroom, TileType.River),
        (TileType.Tundra, TileType.Coral),
        (TileType.Tundra, TileType.River),
        (TileType.Lake, TileType.Pond),
        (TileType.Ruins, TileType.Pond)
    ];

    private static readonly Dictionary<ThemeContext, TileType[]> ThemeTerrains = new()
    {
        [ThemeContext.Temperate] = [TileType.Plains, TileType.Woods, TileType.Meadow, TileType.Village, TileType.Ruins, TileType.Pond, TileType.Lake, TileType.River],
        [ThemeContext.Tropical] = [TileType.River, TileType.Coral, TileType.Woods, TileType.Volcano, TileType.Pond, TileType.Savanna, TileType.Swamp],
        [ThemeContext.Frontier] = [TileType.Desert, TileType.Tundra, TileType.Snow, TileType.Volcano, TileType.Swamp, TileType.Mushroom, TileType.Ruins, TileType.Lake]
    };

    private static readonly TileType[] UniversalTerrains = [TileType.Plains, TileType.Pond, TileType.Lake];

    private static readonly Dictionary<ThemeContext, (TileType A, TileType B)[]> GoodPairs = new()
    {
        [ThemeContext.Temperate] =
        [
            (TileType.Plains, TileType.Woods),
            (TileType.Plains, TileType.Meadow),
            (TileType.Woods, TileType.Lake),
            (TileType.Meadow, TileType.Village),
            (TileType.Village, TileType.Ruins),
            (TileType.Plains, TileType.Pond),
            (TileType.Woods, TileType.Pond),
            (TileType.Lake, TileType.Snow)
        ],
        [ThemeContext.Tropical] =
        [
            (TileType.River, TileType.Woods),
            (TileType.River, TileType.Coral),
            (TileType.Woods, TileType.Savanna),
            (TileType.Volcano, TileType.Woods),
            (TileType.River, TileType.Swamp),
            (TileType.Coral, TileType.Pond),
            (TileType.Savanna, TileType.Plains)
        ],
        [ThemeContext.Frontier] =
        [
            (TileType.Desert, TileType.Savanna),
            (TileType.Tundra, TileType.Snow),
            (TileType.Volcano, TileType.Desert),
            (TileType.Swamp, TileType.Mushroom),
            (TileType.Desert, TileType.Ruins),
            (TileType.Tundra, TileType.Lake)
        ]
    };

    public static SectorPattern Generate(TileType dominantTerrain, ulong seed)
        => Generate(dominantTerrain, seed, ThemeContext.None);

    public static SectorPattern Generate(TileType dominantTerrain, ulong seed, ThemeContext theme)
    {
        var rng = seed;
        var singleTerrain = (Next(ref rng) % 100UL) < 40UL;
        if (singleTerrain)
            return SectorPattern.Pure(dominantTerrain);

        if (!TryPickSecondaryTerrain(dominantTerrain, ref rng, theme, out var secondaryTerrain))
            return SectorPattern.Pure(dominantTerrain);

        var totalWeight = TwoTerrainPatterns.Sum(x => x.Weight);
        var roll = (int)(Next(ref rng) % (ulong)totalWeight);
        var chosen = TwoTerrainPatterns[0];
        var cumulative = 0;
        foreach (var pattern in TwoTerrainPatterns)
        {
            cumulative += pattern.Weight;
            if (roll < cumulative)
            {
                chosen = pattern;
                break;
            }
        }

        var sectors = new TileType[6];
        var cursor = 0;
        var primaryFirst = (chosen.Small, chosen.Large, chosen.Weight) switch
        {
            (2, 4, 10) => true,
            (1, 5, 5) => true,
            _ => false
        };
        var firstTerrain = primaryFirst ? dominantTerrain : secondaryTerrain;
        var secondTerrain = primaryFirst ? secondaryTerrain : dominantTerrain;
        var firstCount = primaryFirst ? chosen.Large : chosen.Small;
        var secondCount = primaryFirst ? chosen.Small : chosen.Large;
        for (var i = 0; i < firstCount; i++)
            sectors[cursor++] = firstTerrain;
        for (var i = 0; i < secondCount; i++)
            sectors[cursor++] = secondTerrain;

        var rotationOffset = (int)(Next(ref rng) % 6UL);
        var rotated = new TileType[6];
        for (var i = 0; i < 6; i++)
            rotated[(i + rotationOffset) % 6] = sectors[i];

        return new SectorPattern(rotated[0], rotated[1], rotated[2], rotated[3], rotated[4], rotated[5]);
    }

    private static bool TryPickSecondaryTerrain(TileType dominantTerrain, ref ulong rng, ThemeContext theme, out TileType secondaryTerrain)
    {
        secondaryTerrain = dominantTerrain;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidatePool = BuildCandidatePool(dominantTerrain, ref rng, theme);
            if (candidatePool.Length == 0)
                break;

            var candidate = candidatePool[(int)(Next(ref rng) % (ulong)candidatePool.Length)];
            if (IsForbiddenPair(dominantTerrain, candidate, theme))
                continue;

            secondaryTerrain = candidate;
            return true;
        }

        return false;
    }

    private static TileType[] BuildCandidatePool(TileType dominantTerrain, ref ulong rng, ThemeContext theme)
    {
        if (theme != ThemeContext.None)
        {
            var goodPairs = GoodPairs.TryGetValue(theme, out var pairs) && pairs is not null
                ? pairs
                : Array.Empty<(TileType A, TileType B)>();
            if (goodPairs.Length > 0 && (Next(ref rng) % 100UL) < 70UL)
            {
                var themedMatches = goodPairs
                    .Where(p => p.A == dominantTerrain || p.B == dominantTerrain)
                    .Select(p => p.A == dominantTerrain ? p.B : p.A)
                    .Distinct()
                    .ToArray();
                if (themedMatches.Length > 0)
                    return themedMatches;
            }

            if ((Next(ref rng) % 100UL) < 80UL && ThemeTerrains.TryGetValue(theme, out var themedPool))
            {
                var sameTheme = themedPool.Where(t => t != dominantTerrain).Distinct().ToArray();
                if (sameTheme.Length > 0)
                    return sameTheme;
            }

            var universal = UniversalTerrains.Where(t => t != dominantTerrain).Distinct().ToArray();
            if (universal.Length > 0)
                return universal;
        }

        return GameState.AllTileTypes.Where(t => t != dominantTerrain).ToArray();
    }

    private static bool IsForbiddenPair(TileType a, TileType b, ThemeContext theme)
    {
        if (theme != ThemeContext.Frontier &&
            ((a == TileType.Snow && b == TileType.Volcano) || (a == TileType.Volcano && b == TileType.Snow)))
            return true;

        return ForbiddenPairs.Contains((a, b)) || ForbiddenPairs.Contains((b, a));
    }

    private static ulong Next(ref ulong value)
    {
        value ^= value >> 12;
        value ^= value << 25;
        value ^= value >> 27;
        value *= 2685821657736338717UL;
        return value;
    }
}
