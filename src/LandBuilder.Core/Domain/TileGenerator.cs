namespace LandBuilder.Domain;

/// <summary>
/// Builds tiles from contiguous terrain segments (e.g. 4 forest + 2 field) so tiles read as natural landscapes.
/// Tiles get slightly busier as the run goes on, terrains with open quests are favoured so quests stay reachable,
/// and once rivers are unlocked some tiles get a channel laid over them.
/// </summary>
public static class TileGenerator
{
    private static readonly (int[] Segments, double BaseWeight, double LateWeight)[] Layouts =
    [
        ([6], 20, 10),
        ([5, 1], 18, 14),
        ([4, 2], 18, 16),
        ([3, 3], 14, 14),
        ([4, 1, 1], 6, 9),
        ([3, 2, 1], 10, 16),
        ([2, 2, 2], 4, 9),
        ([2, 1, 2, 1], 0, 4)
    ];

    private static readonly Dictionary<Terrain, double> TerrainWeights = new()
    {
        [Terrain.Grass] = 26,
        [Terrain.Forest] = 22,
        [Terrain.Field] = 16,
        [Terrain.Village] = 13,
        [Terrain.Water] = 14,
        [Terrain.Mountain] = 11,
        [Terrain.Sand] = 9,
        [Terrain.Snow] = 10,
        [Terrain.Swamp] = 9,
        [Terrain.Orchard] = 11
    };

    public const double RiverTileChance = 0.2;
    public const double RiverQuestBonus = 0.1;

    /// <summary>Pairs that look natural side by side get picked together more often.</summary>
    private static double Affinity(Terrain a, Terrain b)
    {
        static bool Is(Terrain x, Terrain y, Terrain p, Terrain q) => (x == p && y == q) || (x == q && y == p);

        if (Is(a, b, Terrain.Water, Terrain.Sand)) return 2.4;
        if (Is(a, b, Terrain.Snow, Terrain.Mountain)) return 2.2;
        if (Is(a, b, Terrain.Field, Terrain.Village)) return 2.0;
        if (Is(a, b, Terrain.Swamp, Terrain.Water)) return 1.8;
        if (Is(a, b, Terrain.Orchard, Terrain.Village)) return 1.8;
        if (Is(a, b, Terrain.Forest, Terrain.Mountain)) return 1.8;
        if (Is(a, b, Terrain.Orchard, Terrain.Field)) return 1.6;
        if (Is(a, b, Terrain.Snow, Terrain.Forest)) return 1.5;
        if (Is(a, b, Terrain.Water, Terrain.Grass)) return 1.5;
        if (Is(a, b, Terrain.Swamp, Terrain.Forest)) return 1.4;
        if (Is(a, b, Terrain.Forest, Terrain.Grass)) return 1.4;
        if (Is(a, b, Terrain.Orchard, Terrain.Grass)) return 1.3;
        if (Is(a, b, Terrain.Field, Terrain.Grass)) return 1.3;
        if (Is(a, b, Terrain.Mountain, Terrain.Sand)) return 1.2;
        if (Is(a, b, Terrain.Village, Terrain.Water)) return 1.1;
        if (Is(a, b, Terrain.Swamp, Terrain.Snow)) return 0.4;
        if (Is(a, b, Terrain.Swamp, Terrain.Sand)) return 0.4;
        if (Is(a, b, Terrain.Sand, Terrain.Forest)) return 0.4;
        if (Is(a, b, Terrain.Sand, Terrain.Field)) return 0.5;
        if (Is(a, b, Terrain.Snow, Terrain.Sand)) return 0.1;
        if (Is(a, b, Terrain.Snow, Terrain.Orchard)) return 0.2;
        return 1.0;
    }

    public static TileSpec Generate(ref Rng rng, IReadOnlyCollection<Terrain> unlocked, int turn, IReadOnlyCollection<Terrain>? questTerrains = null, WorldTheme theme = WorldTheme.Temperate)
    {
        // Terrains the theme rules out never appear, even if a layout asks for many distinct terrains.
        var land = unlocked.Where(t => t != Terrain.River && Themes.WeightMultiplier(theme, t) > 0).ToArray();
        if (land.Length == 0)
            land = unlocked.Where(t => t != Terrain.River).ToArray();
        if (land.Length == 0)
            throw new ArgumentException("At least one non-river terrain must be unlocked.", nameof(unlocked));

        var lateness = Math.Clamp(turn / 60.0, 0.0, 1.0);
        var layoutOptions = Layouts
            .Where(l => l.Segments.Length <= land.Length)
            .Select(l => (l.Segments, l.BaseWeight + ((l.LateWeight - l.BaseWeight) * lateness)))
            .ToArray();
        var segments = rng.PickWeighted(layoutOptions);

        // Pick one terrain per distinct segment slot. Repeated layouts like [2,1,2,1] reuse terrains A/B alternately.
        var distinctNeeded = segments.Length == 4 ? 2 : segments.Length;
        var chosen = new List<Terrain>(distinctNeeded);
        for (var i = 0; i < distinctNeeded; i++)
        {
            var options = land
                .Where(t => !chosen.Contains(t))
                .Select(t => (t, Weight(t, chosen, questTerrains, theme)))
                .ToArray();
            chosen.Add(rng.PickWeighted(options));
        }

        var sectors = new Terrain[6];
        var cursor = 0;
        for (var s = 0; s < segments.Length; s++)
        {
            var terrain = chosen[s % chosen.Count];
            for (var k = 0; k < segments[s]; k++)
                sectors[cursor++] = terrain;
        }

        var offset = rng.NextInt(6);
        var rotated = new Terrain[6];
        for (var i = 0; i < 6; i++)
            rotated[(i + offset) % 6] = sectors[i];

        if (unlocked.Contains(Terrain.River))
        {
            var chance = RiverTileChance + (questTerrains?.Contains(Terrain.River) == true ? RiverQuestBonus : 0.0);
            if (rng.Chance(chance))
                LayRiver(ref rng, rotated);
        }

        return TileSpec.FromArray(rotated);
    }

    /// <summary>
    /// A tile that suits a blueprint plot: a normal tile with a run of at least <paramref name="min"/> sectors of
    /// <paramref name="terrain"/> laid over it, so wonders do not stall waiting for a rare layout.
    /// </summary>
    public static TileSpec GenerateFor(ref Rng rng, IReadOnlyCollection<Terrain> unlocked, int turn, Terrain terrain, int min, WorldTheme theme)
    {
        var sectors = Generate(ref rng, unlocked, turn, theme: theme).ToArray();
        var run = Math.Clamp(min + rng.NextInt(2), 1, 6);
        var start = rng.NextInt(6);
        for (var i = 0; i < run; i++)
            sectors[(start + i) % 6] = terrain;
        return TileSpec.FromArray(sectors);
    }

    /// <summary>Overwrites one to three edges with river: mostly bends and straights, sometimes a fork or a spring.</summary>
    private static void LayRiver(ref Rng rng, Terrain[] sectors)
    {
        var start = rng.NextInt(6);
        var roll = rng.NextDouble();
        int[] edges;
        if (roll < 0.1)
        {
            edges = [start];
        }
        else if (roll < 0.86)
        {
            int[] turns = [2, 3, 3, 4];
            edges = [start, (start + turns[rng.NextInt(turns.Length)]) % 6];
        }
        else
        {
            edges = [start, (start + 2) % 6, (start + 4) % 6];
        }

        foreach (var e in edges)
            sectors[e] = Terrain.River;
    }

    private static double Weight(Terrain candidate, IReadOnlyList<Terrain> chosen, IReadOnlyCollection<Terrain>? questTerrains, WorldTheme theme)
    {
        var weight = TerrainWeights[candidate] * Themes.WeightMultiplier(theme, candidate);
        foreach (var other in chosen)
            weight *= Affinity(candidate, other);
        if (questTerrains is not null && questTerrains.Contains(candidate))
            weight *= 1.45;
        return weight;
    }
}
