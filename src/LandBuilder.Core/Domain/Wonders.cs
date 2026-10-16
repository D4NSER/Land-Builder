namespace LandBuilder.Domain;

/// <summary>Special landmark tiles. The numeric values are persisted in saves; append, never reorder.</summary>
public enum WonderType
{
    Castle = 0,
    Cathedral = 1,
    GreatPagoda = 2,
    Harbour = 3,
    Lighthouse = 4,
    RocketPad = 5,
    Observatory = 6,
    BotanicalPark = 7
}

/// <summary>
/// A wonder is placed like a tile (its <see cref="Edges"/> match neighbours as usual) but starts as a construction
/// site. It completes once the edges of its neighbours facing it satisfy <see cref="Recipe"/>.
/// </summary>
public sealed record WonderDef(
    WonderType Type,
    string Name,
    string Blurb,
    TileSpec Edges,
    IReadOnlyDictionary<Terrain, int> Recipe,
    int RewardPoints,
    int RewardTiles);

public sealed record WonderProgress(WonderDef Def, IReadOnlyDictionary<Terrain, int> Have, bool Complete);

public static class Wonders
{
    public const int MaxCards = 2;

    private static TileSpec E(Terrain a, Terrain b, Terrain c, Terrain d, Terrain e, Terrain f) => new(a, b, c, d, e, f);

    private static readonly Dictionary<WonderType, WonderDef> Defs = new()
    {
        [WonderType.Castle] = new(WonderType.Castle, "Castle", "A walled keep that crowns a town under the mountains",
            E(Terrain.Village, Terrain.Village, Terrain.Village, Terrain.Village, Terrain.Mountain, Terrain.Mountain),
            new Dictionary<Terrain, int> { [Terrain.Village] = 3, [Terrain.Mountain] = 1 }, 400, 6),
        [WonderType.Cathedral] = new(WonderType.Cathedral, "Cathedral", "Twin spires at the heart of a city",
            E(Terrain.Village, Terrain.Village, Terrain.Village, Terrain.Village, Terrain.Village, Terrain.Village),
            new Dictionary<Terrain, int> { [Terrain.Village] = 5 }, 450, 6),
        [WonderType.GreatPagoda] = new(WonderType.GreatPagoda, "Great Pagoda", "Seven tiers rising over town and grove",
            E(Terrain.Village, Terrain.Village, Terrain.Village, Terrain.Forest, Terrain.Forest, Terrain.Forest),
            new Dictionary<Terrain, int> { [Terrain.Village] = 2, [Terrain.Forest] = 2 }, 380, 6),
        [WonderType.Harbour] = new(WonderType.Harbour, "Harbour", "Docks, warehouses and a tall ship",
            E(Terrain.Water, Terrain.Water, Terrain.Water, Terrain.Village, Terrain.Village, Terrain.Village),
            new Dictionary<Terrain, int> { [Terrain.Water] = 2, [Terrain.Village] = 2 }, 380, 6),
        [WonderType.Lighthouse] = new(WonderType.Lighthouse, "Lighthouse", "A beacon over the lake shore",
            E(Terrain.Water, Terrain.Water, Terrain.Water, Terrain.Water, Terrain.Grass, Terrain.Grass),
            new Dictionary<Terrain, int> { [Terrain.Water] = 3 }, 300, 5),
        [WonderType.RocketPad] = new(WonderType.RocketPad, "Rocket Pad", "A launch tower out on the open plains",
            E(Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Field, Terrain.Field),
            new Dictionary<Terrain, int> { [Terrain.Grass] = 3, [Terrain.Field] = 2 }, 420, 6),
        [WonderType.Observatory] = new(WonderType.Observatory, "Observatory", "A white dome among the peaks",
            E(Terrain.Mountain, Terrain.Mountain, Terrain.Mountain, Terrain.Mountain, Terrain.Forest, Terrain.Forest),
            new Dictionary<Terrain, int> { [Terrain.Mountain] = 3 }, 360, 5),
        [WonderType.BotanicalPark] = new(WonderType.BotanicalPark, "Botanical Park", "A glass palm house in landscaped gardens",
            E(Terrain.Forest, Terrain.Forest, Terrain.Grass, Terrain.Grass, Terrain.Water, Terrain.Water),
            new Dictionary<Terrain, int> { [Terrain.Forest] = 2, [Terrain.Grass] = 2, [Terrain.Water] = 1 }, 380, 6)
    };

    public static IReadOnlyList<WonderType> All { get; } = Enum.GetValues<WonderType>();

    public static WonderDef Info(WonderType type) => Defs[type];

    private static double ThemeWeight(WorldTheme theme, WonderType type) => (theme, type) switch
    {
        (WorldTheme.Japanese, WonderType.GreatPagoda) => 4,
        (WorldTheme.Japanese, WonderType.Cathedral) => 0,
        (_, WonderType.GreatPagoda) => 0.3,
        (WorldTheme.Temperate or WorldTheme.Autumn, WonderType.Cathedral or WonderType.Castle) => 2.5,
        (WorldTheme.Nordic, WonderType.Castle or WonderType.Lighthouse or WonderType.Observatory) => 2.5,
        (WorldTheme.Tropical, WonderType.Harbour or WonderType.Lighthouse or WonderType.BotanicalPark) => 2.5,
        (WorldTheme.Savanna, WonderType.RocketPad or WonderType.Observatory) => 2.5,
        _ => 1
    };

    /// <summary>Wonders whose edges and recipe only use terrains already unlocked on this island.</summary>
    public static IReadOnlyList<WonderType> Available(GameState state) =>
        All.Where(t =>
        {
            var def = Defs[t];
            return def.Edges.DistinctTerrains.All(state.Unlocked.Contains) && def.Recipe.Keys.All(state.Unlocked.Contains);
        }).ToArray();

    public static WonderType? Draw(ref Rng rng, GameState state)
    {
        var options = Available(state)
            .Where(t => !state.WonderCards.Contains(t) && !state.Board.Values.Any(p => p.Wonder == t))
            .Select(t => (t, ThemeWeight(state.Theme, t)))
            .Where(o => o.Item2 > 0)
            .ToArray();
        return options.Length == 0 ? null : rng.PickWeighted(options);
    }

    /// <summary>Counts the neighbours' edges that face the wonder, by terrain, and checks the recipe.</summary>
    public static WonderProgress Progress(GameState state, int slot)
    {
        var placed = state.Board[slot];
        var def = Defs[placed.Wonder ?? throw new ArgumentException("No wonder in this slot.", nameof(slot))];
        var have = new Dictionary<Terrain, int>();
        for (var edge = 0; edge < 6; edge++)
        {
            if (!state.Board.TryGetValue(HexGrid.NeighborSlot(slot, edge), out var neighbor))
                continue;
            var terrain = neighbor.EdgeTerrain(HexGrid.OppositeEdge(edge));
            have[terrain] = have.GetValueOrDefault(terrain) + 1;
        }

        var complete = def.Recipe.All(r => have.GetValueOrDefault(r.Key) >= r.Value);
        return new WonderProgress(def, have, complete);
    }
}
