using System;
using System.Collections.Generic;
using System.Linq;

namespace LandBuilder.Presentation;

/// <summary>
/// What a tile knows about its surroundings, so structures can grow across tiles: the size tier of the village it
/// belongs to, which edges lead a road into a neighbouring village, which edges continue a mountain ridge, and how
/// deep inside a large forest it sits. Edges are in the tile's local (unrotated) frame.
/// </summary>
/// <summary>How a wonder reshapes the ground around it.</summary>
public enum ZoneKind
{
    /// <summary>Props are cleared, the ground is left alone.</summary>
    Clear = 0,

    /// <summary>Land is levelled to <see cref="Zone.Height"/> (water stays water) and cleared.</summary>
    Lot = 1,

    /// <summary>Dug out and flooded, so a ship can float even where the plot was mostly land.</summary>
    Basin = 2
}

/// <summary>A circle in world space (X, Z) that a wonder claims.</summary>
public readonly record struct Zone(float X, float Z, float Radius, ZoneKind Kind, float Height = TerrainSampler.LotHeight)
{
    public string Key => FormattableString.Invariant($"{X:0.00},{Z:0.00},{Radius:0.00},{(int)Kind},{Height:0.00}");
}

public sealed record TileContext(int VillageTier, IReadOnlyList<int> RoadEdges, IReadOnlyList<int> RidgeEdges, int ForestTier)
{
    public static TileContext Empty { get; } = new(0, [], [], 0);

    /// <summary>Edges where this tile's lake flows on into a neighbouring lake or river (no shore there).</summary>
    public IReadOnlyList<int> LakeEdges { get; init; } = [];

    /// <summary>How close this tile is to the heart of its settlement: 1 at the very centre, 0 at the edge.</summary>
    public float Downtown { get; init; }

    /// <summary>Wonder lots, basins and clearings near this tile.</summary>
    public IReadOnlyList<Zone> Zones { get; init; } = [];

    /// <summary>Changes whenever the ground itself has to be rebuilt, not just the props on it.</summary>
    public string TerrainKey => string.Join(",", LakeEdges) + "|" + string.Join(";", Zones.Select(z => z.Key));

    public string Key => $"{VillageTier}|{Downtown:0.0}|{string.Join(",", RoadEdges)}|{string.Join(",", RidgeEdges)}|{ForestTier}|{TerrainKey}";

    public bool HasRoad(int localEdge) => RoadEdges.Contains(localEdge);
}
