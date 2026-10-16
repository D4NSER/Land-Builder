namespace LandBuilder.Domain;

/// <summary>One wedge of a placed tile, addressed by the world edge it touches.</summary>
public readonly record struct SectorRef(int Slot, int Edge);

public sealed record Region(int Id, Terrain Terrain, IReadOnlyList<SectorRef> Sectors, int OpenEdges)
{
    public int Size => Sectors.Count;
    public bool IsClosed => OpenEdges == 0;
}

/// <summary>
/// Connected areas of identical terrain across the board. Adjacent wedges inside a tile join when they share a terrain,
/// and wedges on either side of a shared edge join when their terrains match. A region is closed once none of its
/// wedges border an empty slot, which means it can never grow again. All river wedges of one tile form one channel.
/// </summary>
public sealed class RegionMap
{
    private readonly Dictionary<SectorRef, int> _regionBySector = new();
    private readonly List<Region> _regions = new();

    public IReadOnlyList<Region> Regions => _regions;

    private RegionMap()
    {
    }

    public static RegionMap Build(IReadOnlyDictionary<int, PlacedTile> board)
    {
        var map = new RegionMap();
        var parent = new Dictionary<SectorRef, SectorRef>();

        SectorRef Find(SectorRef x)
        {
            while (true)
            {
                var p = parent[x];
                if (p == x)
                    return x;
                var gp = parent[p];
                parent[x] = gp;
                x = gp;
            }
        }

        void Union(SectorRef a, SectorRef b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra == rb)
                return;
            // Deterministic: smaller key becomes the root.
            if (Compare(ra, rb) < 0)
                parent[rb] = ra;
            else
                parent[ra] = rb;
        }

        foreach (var (slot, _) in board)
        {
            for (var e = 0; e < 6; e++)
            {
                var s = new SectorRef(slot, e);
                parent[s] = s;
            }
        }

        foreach (var (slot, tile) in board)
        {
            // A river network inside one tile is a single channel even when its edges are not adjacent.
            var firstRiver = -1;
            for (var e = 0; e < 6; e++)
            {
                if (tile.EdgeTerrain(e) != Terrain.River)
                    continue;
                if (firstRiver < 0)
                    firstRiver = e;
                else
                    Union(new SectorRef(slot, firstRiver), new SectorRef(slot, e));
            }

            for (var e = 0; e < 6; e++)
            {
                var terrain = tile.EdgeTerrain(e);
                var next = (e + 1) % 6;
                if (tile.EdgeTerrain(next) == terrain)
                    Union(new SectorRef(slot, e), new SectorRef(slot, next));

                // Only link each shared edge once (edges 0..2 point to the "forward" neighbours).
                if (e >= 3)
                    continue;
                var neighborSlot = HexGrid.NeighborSlot(slot, e);
                if (!board.TryGetValue(neighborSlot, out var neighbor))
                    continue;
                var facing = HexGrid.OppositeEdge(e);
                if (neighbor.EdgeTerrain(facing) == terrain)
                    Union(new SectorRef(slot, e), new SectorRef(neighborSlot, facing));
            }
        }

        var members = new Dictionary<SectorRef, List<SectorRef>>();
        foreach (var sector in parent.Keys)
        {
            var root = Find(sector);
            if (!members.TryGetValue(root, out var list))
            {
                list = new List<SectorRef>();
                members[root] = list;
            }

            list.Add(sector);
        }

        var id = 0;
        foreach (var (root, list) in members.OrderBy(kv => kv.Key.Slot).ThenBy(kv => kv.Key.Edge))
        {
            list.Sort(Compare);
            var open = 0;
            foreach (var sector in list)
            {
                if (!board.ContainsKey(HexGrid.NeighborSlot(sector.Slot, sector.Edge)))
                    open++;
            }

            var region = new Region(id, board[root.Slot].EdgeTerrain(root.Edge), list, open);
            map._regions.Add(region);
            foreach (var sector in list)
                map._regionBySector[sector] = id;
            id++;
        }

        return map;
    }

    public Region? RegionAt(SectorRef sector) =>
        _regionBySector.TryGetValue(sector, out var id) ? _regions[id] : null;

    public IEnumerable<Region> RegionsTouchingSlot(int slot) =>
        Enumerable.Range(0, 6)
            .Select(e => RegionAt(new SectorRef(slot, e)))
            .Where(r => r is not null)
            .Select(r => r!)
            .DistinctBy(r => r.Id);

    private static int Compare(SectorRef a, SectorRef b)
    {
        var bySlot = a.Slot.CompareTo(b.Slot);
        return bySlot != 0 ? bySlot : a.Edge.CompareTo(b.Edge);
    }
}
