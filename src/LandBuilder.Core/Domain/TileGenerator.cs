namespace LandBuilder.Domain;

/// <summary>
/// Builds tiles from contiguous terrain segments (e.g. 4 forest + 2 field) so tiles read as natural landscapes.
/// Tiles get slightly busier as the run goes on, and terrains with open quests are favoured so quests stay reachable.
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
        [Terrain.Sand] = 9
    };

    /// <summary>Pairs that look natural side by side get picked together more often.</summary>
    private static double Affinity(Terrain a, Terrain b)
    {
        static bool Is(Terrain x, Terrain y, Terrain p, Terrain q) => (x == p && y == q) || (x == q && y == p);

        if (Is(a, b, Terrain.Water, Terrain.Sand)) return 2.4;
        if (Is(a, b, Terrain.Field, Terrain.Village)) return 2.0;
        if (Is(a, b, Terrain.Forest, Terrain.Mountain)) return 1.8;
        if (Is(a, b, Terrain.Water, Terrain.Grass)) return 1.5;
        if (Is(a, b, Terrain.Forest, Terrain.Grass)) return 1.4;
        if (Is(a, b, Terrain.Field, Terrain.Grass)) return 1.3;
        if (Is(a, b, Terrain.Mountain, Terrain.Sand)) return 1.2;
        if (Is(a, b, Terrain.Village, Terrain.Water)) return 1.1;
        if (Is(a, b, Terrain.Sand, Terrain.Forest)) return 0.4;
        if (Is(a, b, Terrain.Sand, Terrain.Field)) return 0.5;
        return 1.0;
    }

    public static TileSpec Generate(ref Rng rng, IReadOnlyCollection<Terrain> unlocked, int turn, IReadOnlyCollection<Terrain>? questTerrains = null)
    {
        if (unlocked.Count == 0)
            throw new ArgumentException("At least one terrain must be unlocked.", nameof(unlocked));

        var lateness = Math.Clamp(turn / 60.0, 0.0, 1.0);
        var layoutOptions = Layouts
            .Where(l => l.Segments.Length <= unlocked.Count)
            .Select(l => (l.Segments, l.BaseWeight + ((l.LateWeight - l.BaseWeight) * lateness)))
            .ToArray();
        var segments = rng.PickWeighted(layoutOptions);

        // Pick one terrain per distinct segment slot. Repeated layouts like [2,1,2,1] reuse terrains A/B alternately.
        var distinctNeeded = segments.Length == 4 ? 2 : segments.Length;
        var chosen = new List<Terrain>(distinctNeeded);
        for (var i = 0; i < distinctNeeded; i++)
        {
            var options = unlocked
                .Where(t => !chosen.Contains(t))
                .Select(t => (t, Weight(t, chosen, questTerrains)))
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

        return TileSpec.FromArray(rotated);
    }

    private static double Weight(Terrain candidate, IReadOnlyList<Terrain> chosen, IReadOnlyCollection<Terrain>? questTerrains)
    {
        var weight = TerrainWeights[candidate];
        foreach (var other in chosen)
            weight *= Affinity(candidate, other);
        if (questTerrains is not null && questTerrains.Contains(candidate))
            weight *= 1.45;
        return weight;
    }
}
