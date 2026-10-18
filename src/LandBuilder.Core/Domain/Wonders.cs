namespace LandBuilder.Domain;

/// <summary>Special landmarks. The numeric values are persisted in saves; append, never reorder.</summary>
public enum WonderType
{
    Castle = 0,
    Cathedral = 1,
    GreatPagoda = 2,
    Harbour = 3,
    Lighthouse = 4,
    RocketPad = 5,
    Observatory = 6,
    BotanicalPark = 7,
    TallShip = 8,
    SequoiaPark = 9
}

/// <summary>
/// One hex of a wonder's footprint. <see cref="Steps"/> walks from the anchor along grid directions (rotated with the
/// blueprint); the tile there needs at least <see cref="Min"/> sectors of <see cref="Terrain"/>.
/// </summary>
public sealed record WonderCell(int[] Steps, Terrain Terrain, int Min);

/// <summary>
/// A wonder covers several hexes. Its card lays down a blueprint; every plot then needs a tile that suits it (the
/// ship's plots each need some water, the castle's need town and mountain...). Plots may also be laid over tiles that
/// already suit them, so a well-built island can raise a wonder at once.
/// </summary>
public sealed record WonderDef(
    WonderType Type,
    string Name,
    string Blurb,
    IReadOnlyList<WonderCell> Cells,
    int RewardPoints,
    int RewardTiles)
{
    public int Size => Cells.Count;
}

/// <summary>A blueprint on the board. Its plots are <see cref="Wonders.Footprint"/>.</summary>
public sealed record WonderSite(int Id, WonderType Type, int Anchor, int Rotation, int Turn, bool Built = false);

public sealed record PlotStatus(int Slot, WonderCell Rule, bool Filled, bool Suits);

public sealed record WonderProgress(WonderDef Def, WonderSite Site, IReadOnlyList<PlotStatus> Plots)
{
    public int Done => Plots.Count(p => p.Filled && p.Suits);
    public bool Complete => Plots.All(p => p.Filled && p.Suits);
}

/// <summary>Whether a blueprint can go down at a spot, plot by plot.</summary>
public sealed record SitePlan(
    WonderType Type,
    int Anchor,
    int Rotation,
    bool IsValid,
    PlacementRejectionReason? RejectionReason,
    IReadOnlyList<PlotStatus> Plots)
{
    public int Ready => Plots.Count(p => p.Filled && p.Suits);
    public bool CompletesAtOnce => IsValid && Plots.All(p => p.Filled && p.Suits);
}

public static class Wonders
{
    public const int MaxCards = 2;

    private static WonderCell C(Terrain terrain, int min, params int[] steps) => new(steps, terrain, min);

    // Footprints radiate from the anchor plot (step = grid direction, rotated with the blueprint), so each wonder
    // spreads over a whole neighbourhood rather than a strip of tiles.
    private static readonly Dictionary<WonderType, WonderDef> Defs = new()
    {
        [WonderType.Castle] = new(WonderType.Castle, "Castle", "A walled keep crowning a town beneath the mountains",
            [C(Terrain.Village, 3), C(Terrain.Village, 2, 0), C(Terrain.Village, 2, 1), C(Terrain.Village, 2, 5), C(Terrain.Mountain, 2, 3)], 600, 8),
        [WonderType.Cathedral] = new(WonderType.Cathedral, "Cathedral", "Twin spires at the heart of a city",
            [C(Terrain.Village, 3), C(Terrain.Village, 3, 0), C(Terrain.Village, 3, 2), C(Terrain.Village, 3, 4)], 560, 7),
        [WonderType.GreatPagoda] = new(WonderType.GreatPagoda, "Great Pagoda", "Seven tiers rising over town and grove",
            [C(Terrain.Village, 3), C(Terrain.Village, 2, 1), C(Terrain.Forest, 2, 0), C(Terrain.Forest, 2, 2), C(Terrain.Forest, 2, 4)], 600, 8),
        [WonderType.Harbour] = new(WonderType.Harbour, "Harbour", "Docks, warehouses and cranes on a town's waterfront",
            [C(Terrain.Village, 2), C(Terrain.Village, 2, 2), C(Terrain.Village, 2, 4), C(Terrain.Water, 2, 0), C(Terrain.Water, 2, 1)], 620, 8),
        [WonderType.Lighthouse] = new(WonderType.Lighthouse, "Lighthouse", "A beacon on a headland, water on three sides",
            [C(Terrain.Grass, 2), C(Terrain.Water, 2, 0), C(Terrain.Water, 2, 2), C(Terrain.Water, 2, 4)], 480, 6),
        [WonderType.RocketPad] = new(WonderType.RocketPad, "Rocket Pad", "A launch tower out on the open plains",
            [C(Terrain.Grass, 3), C(Terrain.Grass, 3, 1), C(Terrain.Grass, 2, 4), C(Terrain.Field, 2, 0), C(Terrain.Field, 2, 3)], 620, 8),
        [WonderType.Observatory] = new(WonderType.Observatory, "Observatory", "A white dome high among the peaks",
            [C(Terrain.Mountain, 3), C(Terrain.Mountain, 3, 0), C(Terrain.Mountain, 3, 2), C(Terrain.Mountain, 2, 4)], 560, 7),
        [WonderType.BotanicalPark] = new(WonderType.BotanicalPark, "Botanical Park", "A glass palm house in landscaped gardens",
            [C(Terrain.Grass, 3), C(Terrain.Grass, 2, 0), C(Terrain.Forest, 2, 1), C(Terrain.Forest, 2, 3), C(Terrain.Water, 1, 5)], 600, 8),
        [WonderType.TallShip] = new(WonderType.TallShip, "Tall Ship", "A three-masted galleon riding at anchor",
            [C(Terrain.Water, 1), C(Terrain.Water, 1, 0), C(Terrain.Water, 1, 0, 0), C(Terrain.Water, 1, 1), C(Terrain.Water, 1, 0, 1), C(Terrain.Water, 1, 3)], 720, 9),
        [WonderType.SequoiaPark] = new(WonderType.SequoiaPark, "Sequoia Park", "A grove of giant redwoods around a ranger lodge",
            [C(Terrain.Forest, 3), C(Terrain.Forest, 3, 0), C(Terrain.Forest, 3, 1), C(Terrain.Forest, 2, 2), C(Terrain.Forest, 2, 3), C(Terrain.Grass, 2, 4)], 720, 9)
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
        (WorldTheme.Tropical, WonderType.Harbour or WonderType.TallShip or WonderType.BotanicalPark) => 2.5,
        (WorldTheme.Nordic or WorldTheme.Autumn or WorldTheme.Temperate, WonderType.SequoiaPark) => 2,
        (WorldTheme.Savanna, WonderType.RocketPad or WonderType.Observatory) => 2.5,
        _ => 1
    };

    /// <summary>Wonders whose plots only ask for terrains already unlocked on this island.</summary>
    public static IReadOnlyList<WonderType> Available(GameState state) =>
        All.Where(t => Defs[t].Cells.All(c => state.Unlocked.Contains(c.Terrain))).ToArray();

    public static WonderType? Draw(ref Rng rng, GameState state)
    {
        var options = Available(state)
            .Where(t => !state.WonderCards.Contains(t) && !state.Sites.Any(s => s.Type == t))
            .Select(t => (t, ThemeWeight(state.Theme, t)))
            .Where(o => o.Item2 > 0)
            .ToArray();
        return options.Length == 0 ? null : rng.PickWeighted(options);
    }

    /// <summary>The slots a blueprint covers, in the same order as its definition's cells.</summary>
    public static int[] Footprint(WonderType type, int anchor, int rotation)
    {
        var cells = Defs[type].Cells;
        var result = new int[cells.Count];
        var origin = HexGrid.Decode(anchor);
        for (var i = 0; i < cells.Count; i++)
        {
            var coord = origin;
            foreach (var step in cells[i].Steps)
                coord += HexGrid.Direction(step + rotation);
            result[i] = HexGrid.Encode(coord);
        }

        return result;
    }

    public static int[] Footprint(WonderSite site) => Footprint(site.Type, site.Anchor, site.Rotation);

    /// <summary>Sectors of the rule's terrain on a tile; river channels count as water.</summary>
    public static int Count(TileSpec tile, Terrain terrain)
    {
        var count = 0;
        for (var i = 0; i < 6; i++)
        {
            var t = tile[i];
            if (t == terrain || (terrain == Terrain.Water && t == Terrain.River))
                count++;
        }

        return count;
    }

    public static bool Suits(WonderCell rule, TileSpec tile) => Count(tile, rule.Terrain) >= rule.Min;

    public static WonderProgress Progress(GameState state, WonderSite site)
    {
        var def = Defs[site.Type];
        var slots = Footprint(site);
        var plots = new PlotStatus[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            var filled = state.Board.TryGetValue(slots[i], out var placed);
            plots[i] = new PlotStatus(slots[i], def.Cells[i], filled, filled && Suits(def.Cells[i], placed!.Tile));
        }

        return new WonderProgress(def, site, plots);
    }

    /// <summary>The unfinished blueprint plot at <paramref name="slot"/>, if any.</summary>
    public static (WonderSite Site, WonderCell Rule)? PlotAt(GameState state, int slot)
    {
        foreach (var site in state.Sites)
        {
            if (site.Built)
                continue;
            var slots = Footprint(site);
            var index = Array.IndexOf(slots, slot);
            if (index >= 0)
                return (site, Defs[site.Type].Cells[index]);
        }

        return null;
    }

    /// <summary>Finished wonders whose footprint borders <paramref name="slot"/> (the slot itself not included).</summary>
    public static int BuiltNeighbours(GameState state, int slot)
    {
        var count = 0;
        foreach (var site in state.Sites)
        {
            if (!site.Built)
                continue;
            var footprint = Footprint(site);
            if (Array.IndexOf(footprint, slot) < 0 && HexGrid.GetNeighborSlots(slot).Any(n => Array.IndexOf(footprint, n) >= 0))
                count++;
        }

        return count;
    }

    /// <summary>Every slot claimed by any blueprint or finished wonder.</summary>
    public static HashSet<int> ClaimedSlots(GameState state) => state.Sites.SelectMany(Footprint).ToHashSet();

    /// <summary>
    /// Checks a blueprint spot: plots must be free of other wonders, existing tiles under it must already suit their
    /// plot, and the footprint has to touch (or sit on) the island.
    /// </summary>
    public static SitePlan Plan(GameState state, WonderType type, int anchor, int rotation)
    {
        rotation = GameEngine.NormalizeRotation(rotation);
        var def = Defs[type];
        var slots = Footprint(type, anchor, rotation);
        var claimed = ClaimedSlots(state);
        var plots = new PlotStatus[slots.Length];
        PlacementRejectionReason? reason = null;
        var touches = false;
        for (var i = 0; i < slots.Length; i++)
        {
            var filled = state.Board.TryGetValue(slots[i], out var placed);
            var suits = filled && Suits(def.Cells[i], placed!.Tile);
            plots[i] = new PlotStatus(slots[i], def.Cells[i], filled, suits);
            if (claimed.Contains(slots[i]))
                reason ??= PlacementRejectionReason.SiteBlocked;
            else if (filled && !suits)
                reason ??= PlacementRejectionReason.SiteBlocked;
            touches |= filled || HexGrid.GetNeighborSlots(slots[i]).Any(state.Board.ContainsKey);
        }

        if (!touches)
            reason ??= PlacementRejectionReason.NotConnected;
        return new SitePlan(type, anchor, rotation, reason is null, reason, plots);
    }
}
