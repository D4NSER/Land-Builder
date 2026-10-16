using System.Collections.Generic;
using System.Linq;

namespace LandBuilder.Presentation;

/// <summary>
/// What a tile knows about its surroundings, so structures can grow across tiles: the size tier of the village it
/// belongs to, which edges lead a road into a neighbouring village, which edges continue a mountain ridge, and how
/// deep inside a large forest it sits. Edges are in the tile's local (unrotated) frame.
/// </summary>
public sealed record TileContext(int VillageTier, IReadOnlyList<int> RoadEdges, IReadOnlyList<int> RidgeEdges, int ForestTier)
{
    public static TileContext Empty { get; } = new(0, [], [], 0);

    public string Key => $"{VillageTier}|{string.Join(",", RoadEdges)}|{string.Join(",", RidgeEdges)}|{ForestTier}";

    public bool HasRoad(int localEdge) => RoadEdges.Contains(localEdge);
}
