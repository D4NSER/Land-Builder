namespace LandBuilder.Domain;

public readonly record struct HexCoord(int Q, int R);

public static class HexGrid
{
    private static readonly HexCoord[] Directions =
    {
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, 0),
        new(-1, 1),
        new(0, 1)
    };

    private static readonly HexCoord[] UniquePairDirections =
    {
        new(1, 0),
        new(1, -1),
        new(0, -1)
    };

    public static int Encode(HexCoord coord) => unchecked((coord.Q << 16) | (coord.R & 0xFFFF));

    public static HexCoord Decode(int slotIndex) => new(slotIndex >> 16, (short)(slotIndex & 0xFFFF));

    public static int OriginSlotIndex => Encode(new HexCoord(0, 0));

    public static IEnumerable<int> GetNeighborSlots(int slotIndex)
    {
        var coord = Decode(slotIndex);
        foreach (var direction in Directions)
            yield return Encode(new HexCoord(coord.Q + direction.Q, coord.R + direction.R));
    }

    public static IEnumerable<int> GetUniqueNeighborPairs(int slotIndex)
    {
        var coord = Decode(slotIndex);
        foreach (var direction in UniquePairDirections)
            yield return Encode(new HexCoord(coord.Q + direction.Q, coord.R + direction.R));
    }
}
