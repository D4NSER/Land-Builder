namespace LandBuilder.Domain;

/// <summary>
/// A hex tile split into six wedge-shaped sectors. Sector <c>i</c> owns edge <c>i</c> when the tile is unrotated.
/// </summary>
public readonly record struct TileSpec(Terrain S0, Terrain S1, Terrain S2, Terrain S3, Terrain S4, Terrain S5)
{
    public Terrain this[int sector] => (((sector % 6) + 6) % 6) switch
    {
        0 => S0,
        1 => S1,
        2 => S2,
        3 => S3,
        4 => S4,
        _ => S5
    };

    public static TileSpec Pure(Terrain terrain) => new(terrain, terrain, terrain, terrain, terrain, terrain);

    public static TileSpec FromArray(IReadOnlyList<Terrain> sectors)
    {
        if (sectors.Count != 6)
            throw new ArgumentException("A tile needs exactly six sectors.", nameof(sectors));
        return new TileSpec(sectors[0], sectors[1], sectors[2], sectors[3], sectors[4], sectors[5]);
    }

    public Terrain[] ToArray() => [S0, S1, S2, S3, S4, S5];

    /// <summary>
    /// Terrain shown on world edge <paramref name="edge"/> after rotating the tile by <paramref name="rotation"/> sixth-turns.
    /// Rotating by +1 moves sector 0 onto edge 1.
    /// </summary>
    public Terrain EdgeTerrain(int edge, int rotation) => this[edge - rotation];

    public bool IsPure => S0 == S1 && S1 == S2 && S2 == S3 && S3 == S4 && S4 == S5;

    public Terrain Dominant => ToArray()
        .GroupBy(t => t)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.Key)
        .First()
        .Key;

    public IEnumerable<Terrain> DistinctTerrains => ToArray().Distinct();

    public override string ToString() => string.Join("/", ToArray());
}

/// <param name="Wonder">Set when this slot holds a wonder; <paramref name="Built"/> flips once its recipe is met.</param>
public sealed record PlacedTile(TileSpec Tile, int Rotation, int Turn, WonderType? Wonder = null, bool Built = false)
{
    public Terrain EdgeTerrain(int edge) => Tile.EdgeTerrain(edge, Rotation);
}
