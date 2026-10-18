namespace LandBuilder.Domain;

public readonly record struct HexCoord(int Q, int R)
{
    public static HexCoord operator +(HexCoord a, HexCoord b) => new(a.Q + b.Q, a.R + b.R);

    public int DistanceTo(HexCoord other)
    {
        var dq = Q - other.Q;
        var dr = R - other.R;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
    }
}

/// <summary>
/// Axial hex grid helpers. Slots are axial coordinates packed into an int so they can key dictionaries and saves.
/// Edge/direction index <c>i</c> points from a tile towards the neighbour in <see cref="Directions"/>[i];
/// the opposite edge is always <c>(i + 3) % 6</c>.
/// </summary>
public static class HexGrid
{
    public const int EdgeCount = 6;

    private static readonly HexCoord[] Directions =
    [
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, 0),
        new(-1, 1),
        new(0, 1)
    ];

    public static int Encode(HexCoord coord) => unchecked((coord.Q << 16) | (coord.R & 0xFFFF));

    public static HexCoord Decode(int slotIndex) => new(slotIndex >> 16, (short)(slotIndex & 0xFFFF));

    public static int OriginSlotIndex => Encode(new HexCoord(0, 0));

    public static HexCoord Direction(int edge) => Directions[((edge % 6) + 6) % 6];

    public static int OppositeEdge(int edge) => (edge + 3) % 6;

    public static int NeighborSlot(int slotIndex, int edge) => Encode(Decode(slotIndex) + Direction(edge));

    public static IEnumerable<int> GetNeighborSlots(int slotIndex)
    {
        var coord = Decode(slotIndex);
        foreach (var direction in Directions)
            yield return Encode(coord + direction);
    }

    /// <summary>Returns the edge index of <paramref name="slotIndex"/> that faces <paramref name="neighborSlot"/>, or -1.</summary>
    public static int EdgeFacing(int slotIndex, int neighborSlot)
    {
        var source = Decode(slotIndex);
        var target = Decode(neighborSlot);
        var delta = new HexCoord(target.Q - source.Q, target.R - source.R);
        return Array.IndexOf(Directions, delta);
    }
}
