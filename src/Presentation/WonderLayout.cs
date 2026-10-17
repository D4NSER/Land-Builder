using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Where a multi-tile wonder stands: the centre of its footprint, how large and which way round its landmark is
/// drawn, and which circles of ground it levels, floods or clears. Shared by the board (models) and the tile
/// context (terrain and props), so both always agree.
/// </summary>
public static class WonderLayout
{
    public static Vector3 Centroid(WonderType type, int anchor, int rotation)
    {
        var slots = Wonders.Footprint(type, anchor, rotation);
        return slots.Aggregate(Vector3.Zero, (acc, s) => acc + HexLayout.SlotToWorld(s)) / slots.Length;
    }

    public static Vector3 Centroid(WonderSite site) => Centroid(site.Type, site.Anchor, site.Rotation);

    /// <summary>Landmark models are authored at tile size and drawn larger than life over the footprint.</summary>
    public static float Scale(WonderType type) => type switch
    {
        WonderType.TallShip => 1f,
        WonderType.Lighthouse => 1.8f,
        _ => Wonders.Info(type).Size >= 4 ? 2.25f : 2.0f
    };

    /// <summary>Height the lot is levelled to; the observatory sits on a raised plateau among its peaks.</summary>
    public static float LotHeight(WonderType type) => type == WonderType.Observatory ? 0.28f : TerrainSampler.LotHeight;

    /// <summary>
    /// The terrain each model was authored to face, and the direction it faced. The model is turned so that side
    /// looks towards the plots of that terrain (the harbour's docks reach into its lake plots, and so on).
    /// </summary>
    private static (Terrain Terrain, float Degrees)? Facing(WonderType type) => type switch
    {
        WonderType.Castle => (Terrain.Mountain, 270f),
        WonderType.Harbour => (Terrain.Water, 60f),
        WonderType.Lighthouse => (Terrain.Water, 90f),
        WonderType.RocketPad => (Terrain.Field, 270f),
        WonderType.BotanicalPark => (Terrain.Water, 270f),
        WonderType.GreatPagoda => (Terrain.Forest, 240f),
        _ => null
    };

    public static float Yaw(WonderType type, int anchor, int rotation)
    {
        var slots = Wonders.Footprint(type, anchor, rotation);
        var centre = Centroid(type, anchor, rotation);
        if (type == WonderType.TallShip)
        {
            // The hull lies along the footprint's long axis.
            var axis = HexLayout.SlotToWorld(slots[2]) - HexLayout.SlotToWorld(slots[0]);
            return Mathf.Atan2(-axis.Z, axis.X);
        }

        if (Facing(type) is not var (terrain, degrees))
            return HexLayout.RotationToYaw(rotation);

        var cells = Wonders.Info(type).Cells;
        var dir = Vector3.Zero;
        for (var i = 0; i < slots.Length; i++)
        {
            if (cells[i].Terrain == terrain)
                dir += HexLayout.SlotToWorld(slots[i]) - centre;
        }

        // Plots of that terrain on opposite sides cancel out; then face the first one.
        if (dir.LengthSquared() < 0.01f)
            dir = HexLayout.SlotToWorld(slots[cells.ToList().FindIndex(c => c.Terrain == terrain)]) - centre;
        return Mathf.Atan2(-dir.Z, dir.X) - Mathf.DegToRad(degrees);
    }

    public static float Yaw(WonderSite site) => Yaw(site.Type, site.Anchor, site.Rotation);

    /// <summary>Ground a site claims. Unfinished sites only clear room for scaffolding on plots already filled.</summary>
    public static IEnumerable<Zone> Zones(WonderSite site, IReadOnlyCollection<int> filled)
    {
        var slots = Wonders.Footprint(site);
        var cells = Wonders.Info(site.Type).Cells;
        if (!site.Built)
        {
            foreach (var slot in slots.Where(filled.Contains))
            {
                var p = HexLayout.SlotToWorld(slot);
                yield return new Zone(p.X, p.Z, 0.42f, ZoneKind.Clear);
            }

            yield break;
        }

        var centre = Centroid(site);
        var lot = LotHeight(site.Type);
        if (site.Type == WonderType.TallShip)
        {
            var yaw = Yaw(site);
            var axis = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
            for (var i = -2; i <= 2; i++)
            {
                var p = centre + (axis * (i * 0.55f));
                yield return new Zone(p.X, p.Z, 0.62f, ZoneKind.Basin);
            }
        }
        else
        {
            var radius = site.Type == WonderType.Lighthouse ? 0.8f : cells.Count >= 4 ? 1.4f : 1.25f;
            yield return new Zone(centre.X, centre.Z, radius, ZoneKind.Lot, lot);
        }

        for (var i = 0; i < slots.Length; i++)
        {
            var p = HexLayout.SlotToWorld(slots[i]);
            var land = cells[i].Terrain != Terrain.Water && site.Type != WonderType.TallShip;
            yield return new Zone(p.X, p.Z, land ? 0.72f : 0.6f, land ? ZoneKind.Lot : ZoneKind.Clear, lot);
        }
    }

    /// <summary>Plot centres relative to the footprint centre, with the rule each one follows.</summary>
    public static List<(Vector3 Offset, WonderCell Rule)> Plots(WonderSite site)
    {
        var slots = Wonders.Footprint(site);
        var cells = Wonders.Info(site.Type).Cells;
        var centre = Centroid(site);
        return slots.Select((s, i) => (HexLayout.SlotToWorld(s) - centre, cells[i])).ToList();
    }
}
