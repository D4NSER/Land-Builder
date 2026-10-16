using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Evaluates height and colour across one tile. Sector terrains are blended with soft, wobbly wedges, all noise is
/// sampled in world space, and rim heights follow a shared world-space function, so neighbouring tiles continue each
/// other with no visible seam. River sectors become banks of the tile's main land terrain with a channel carved
/// from each river edge to the tile centre.
/// </summary>
public sealed class TerrainSampler
{
    public const float WaterLevel = -0.045f;
    public const float WaterRim = -0.17f;
    public const float RiverHalfWidth = 0.13f;
    private const float BlendWidth = 0.13f;

    private readonly Terrain[] _sectors;
    private readonly Transform3D _toWorld;
    private readonly ThemeStyle _style;
    private readonly List<Vector2> _riverPath = new();
    private readonly bool _riverSpring;

    /// <summary>Height of the flattened building lot on wonder tiles.</summary>
    public const float LotHeight = 0.05f;

    private readonly bool _wonderLot;

    public TerrainSampler(TileSpec tile, Transform3D toWorld, ThemeStyle style, bool wonderLot = false)
    {
        _wonderLot = wonderLot;
        _sectors = tile.ToArray();
        _toWorld = toWorld;
        _style = style;
        BankTerrain = _sectors
            .Where(t => t is not (Terrain.River or Terrain.Water))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .DefaultIfEmpty(Terrain.Grass)
            .First();

        var riverEdges = Enumerable.Range(0, 6).Where(k => _sectors[k] == Terrain.River).ToArray();
        _riverSpring = riverEdges.Length == 1;
        BuildRiverPath(riverEdges);
    }

    public ThemeStyle Style => _style;

    public Vector3 ToWorld(float x, float y, float z) => _toWorld * new Vector3(x, y, z);

    public Transform3D ToLocalTransform => _toWorld.AffineInverse();

    /// <summary>Land terrain that river banks borrow (the tile's dominant land, or meadow).</summary>
    public Terrain BankTerrain { get; }

    public bool HasRiver => _riverPath.Count > 0;

    public Terrain SectorTerrain(int sector) => _sectors[((sector % 6) + 6) % 6];

    /// <summary>The terrain props should treat a sector as (river sectors grow bank props).</summary>
    public Terrain PropTerrain(int sector)
    {
        var t = SectorTerrain(sector);
        return t == Terrain.River ? BankTerrain : t;
    }

    public readonly struct Sample
    {
        public readonly float Height;
        public readonly Color Color;
        public readonly float Wet;
        public readonly float Channel;

        /// <summary>0 for clear lake water, up to 1 for murky marsh puddles.</summary>
        public readonly float Murk;

        public Sample(float height, Color color, float wet, float channel, float murk = 0f)
        {
            Murk = murk;
            Height = height;
            Color = color;
            Wet = wet;
            Channel = channel;
        }
    }

    // ------------------------------------------------------------------ rivers

    private void BuildRiverPath(int[] edges)
    {
        if (edges.Length == 0)
            return;

        Vector2 Mid(int k)
        {
            var d = HexLayout.EdgeDirection(k) * HexLayout.InRadius;
            return new Vector2(d.X, d.Z);
        }

        // A gentle world-space meander that vanishes at the rim so channels line up with neighbours.
        Vector2 Meander(Vector2 p, Vector2 tangent, float t)
        {
            var w = _toWorld * new Vector3(p.X, 0, p.Y);
            var n = (Noise.Fbm(w.X * 1.7f + 13f, w.Z * 1.7f - 5f, 2, 77) - 0.5f) * 0.18f;
            var normal = new Vector2(-tangent.Y, tangent.X);
            return p + (normal * n * Mathf.Sin(Mathf.Pi * t));
        }

        void AddCurve(Vector2 a, Vector2 control, Vector2 b, int steps)
        {
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var p = ((1 - t) * (1 - t) * a) + (2 * (1 - t) * t * control) + (t * t * b);
                var tangent = ((2 * (1 - t) * (control - a)) + (2 * t * (b - control))).Normalized();
                _riverPath.Add(Meander(p, tangent, t));
            }
        }

        if (edges.Length == 2)
        {
            AddCurve(Mid(edges[0]), Vector2.Zero, Mid(edges[1]), 24);
            return;
        }

        // Springs and forks: separate arms from each edge into the middle.
        foreach (var e in edges)
        {
            if (_riverPath.Count > 0)
                _riverPath.Add(new Vector2(float.NaN, float.NaN));
            AddCurve(Mid(e), Mid(e) * 0.5f, Vector2.Zero, 12);
        }
    }

    /// <summary>1 inside the river channel, falling to 0 on the banks.</summary>
    public float ChannelFactor(float x, float z)
    {
        if (_riverPath.Count == 0)
            return 0f;

        var p = new Vector2(x, z);
        var best = float.MaxValue;
        for (var i = 0; i < _riverPath.Count - 1; i++)
        {
            var a = _riverPath[i];
            var b = _riverPath[i + 1];
            if (float.IsNaN(a.X) || float.IsNaN(b.X))
                continue;
            best = Mathf.Min(best, DistanceToSegment(p, a, b));
        }

        var rimFade = Mathf.SmoothStep(0f, 0.25f, HexLayout.DistanceToHexEdge(x, z));
        var world = _toWorld * new Vector3(x, 0, z);
        var width = RiverHalfWidth + ((Noise.Value(world.X * 3f, world.Z * 3f, 91) - 0.5f) * 0.04f * rimFade);
        if (_riverSpring)
            width = Mathf.Max(width, 0.2f - (p.Length() * 0.5f));
        return 1f - Mathf.SmoothStep(width - 0.035f, width + 0.05f, best);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return p.DistanceTo(a + (ab * t));
    }

    // ------------------------------------------------------------------ blending

    /// <summary>Weight of each terrain at a local point (indexed by <see cref="Terrain"/>), summing to 1. River wedges count as bank land.</summary>
    public void TerrainWeights(float x, float z, Span<float> weights)
    {
        weights.Clear();
        var rho = Mathf.Sqrt((x * x) + (z * z));
        var angle = HexLayout.PointAngle(x, z);
        var total = 0f;
        // Wobble borders in world space so transitions look hand-drawn rather than ruler-straight.
        var world = _toWorld * new Vector3(x, 0, z);
        var wobble = ((Noise.Fbm(world.X * 2.4f + 3.1f, world.Z * 2.4f - 7.7f, 2, 41) - 0.5f) * 0.22f) * Mathf.SmoothStep(0.05f, 0.35f, rho);
        for (var k = 0; k < 6; k++)
        {
            var delta = Mathf.Wrap(angle - HexLayout.EdgeAngle(k), -Mathf.Pi, Mathf.Pi);
            var abs = Mathf.Abs(delta);
            const float half = Mathf.Pi / 6f;
            float signed;
            if (abs <= half)
                signed = rho * Mathf.Sin(half - abs);
            else if (abs - half < Mathf.Pi / 2f)
                signed = -rho * Mathf.Sin(abs - half);
            else
                signed = -rho;

            var w = Mathf.SmoothStep(-BlendWidth, BlendWidth, signed + (wobble * (k % 2 == 0 ? 1f : -1f)));
            var terrain = _sectors[k] == Terrain.River ? BankTerrain : _sectors[k];
            weights[(int)terrain] += w;
            total += w;
        }

        if (total <= 0.0001f)
        {
            weights[(int)BankTerrain] = 1f;
            return;
        }

        for (var i = 0; i < weights.Length; i++)
            weights[i] /= total;
    }

    public Sample Evaluate(float x, float z)
    {
        Span<float> w = stackalloc float[Terrains.Count];
        TerrainWeights(x, z, w);
        var world = _toWorld * new Vector3(x, 0, z);
        var wx = world.X;
        var wz = world.Z;
        var edge = HexLayout.DistanceToHexEdge(x, z);
        var interior = Mathf.SmoothStep(0.0f, 0.3f, edge);
        var n = Noise.Fbm(wx * 1.6f, wz * 1.6f, 3, 3);
        var fine = Noise.Value(wx * 7.0f, wz * 7.0f, 11);
        var landRim = 0.03f + (n * 0.04f);

        var height = 0f;
        var r = 0f;
        var g = 0f;
        var b = 0f;
        for (var i = 0; i < w.Length; i++)
        {
            if (w[i] < 0.0005f)
                continue;
            var (h, c) = TerrainHeightColor((Terrain)i, wx, wz, n, fine, interior, edge, landRim);
            height += h * w[i];
            r += c.R * w[i];
            g += c.G * w[i];
            b += c.B * w[i];
        }

        var color = new Color(r, g, b);
        var water = w[(int)Terrain.Water];
        var swamp = w[(int)Terrain.Swamp];

        if (_wonderLot)
        {
            // Level a building lot in the middle; lakes stay lakes so harbours keep their water.
            var lot = Mathf.SmoothStep(0.7f, 0.5f, Mathf.Sqrt((x * x) + (z * z))) * (1f - water);
            height = Mathf.Lerp(height, LotHeight, lot);
        }

        var channel = ChannelFactor(x, z);
        if (channel > 0f)
        {
            var bed = -0.2f + (n * 0.02f);
            height = Mathf.Lerp(height, bed, channel);
            color = color.Lerp(_style.GroundB(Terrain.River), channel);
        }

        // Sandy shoreline where land dips to the water line.
        var paddy = _style.RicePaddies ? w[(int)Terrain.Field] : 0f;
        var wet = Mathf.Max(Mathf.Max(water, channel), Mathf.Max(swamp > 0.2f ? swamp : 0f, paddy > 0.3f && height < WaterLevel + 0.01f ? paddy : 0f));
        if (wet > 0.02f)
        {
            var shore = 1f - Mathf.Clamp(Mathf.Abs(height - WaterLevel) / 0.05f, 0f, 1f);
            color = color.Lerp(_style.Shore, shore * 0.7f * (1f - (swamp * 0.8f)));
        }

        var murk = Mathf.Max(Mathf.Clamp((swamp - water - channel) * 1.4f, 0f, 1f), paddy * 0.55f);
        return new Sample(height, color, wet, channel, murk);
    }

    private (float Height, Color Color) TerrainHeightColor(Terrain terrain, float wx, float wz, float n, float fine, float interior, float edge, float landRim)
    {
        var a = _style.GroundA(terrain);
        var b = _style.GroundB(terrain);
        switch (terrain)
        {
            case Terrain.Grass:
            {
                var h = 0.03f + (n * 0.05f);
                var patch = Noise.Fbm(wx * 0.9f + 40f, wz * 0.9f, 2, 5);
                var c = a.Lerp(b, Mathf.SmoothStep(0.45f, 0.75f, patch)).Darkened((fine - 0.5f) * 0.08f);
                return (Rim(h, landRim, interior), c);
            }
            case Terrain.Forest:
            {
                var h = 0.035f + (n * 0.06f);
                var c = a.Lerp(b, n).Darkened((fine - 0.5f) * 0.1f);
                return (Rim(h, landRim, interior), c);
            }
            case Terrain.Field when _style.RicePaddies:
            {
                // Flooded paddies separated by low grassy bunds.
                var bund = PaddyBund(wx, wz);
                var h = Mathf.Lerp(WaterLevel - 0.03f, 0.03f + (n * 0.01f), bund);
                var c = new Color("6f7f4f").Lerp(a, bund);
                return (Rim(h, landRim, interior), c);
            }
            case Terrain.Field:
            {
                // Patchwork of crops: each world cell gets its own crop colour and furrow direction.
                var cx = Mathf.FloorToInt((wx * 1.25f) + (Noise.Value(wx * 2f, wz * 2f, 9) * 0.6f));
                var cz = Mathf.FloorToInt((wz * 1.25f) + (Noise.Value(wx * 2f + 7f, wz * 2f, 9) * 0.6f));
                var pick = Noise.Hash01(cx, cz, 21);
                var crop = pick < 0.55f ? a : pick < 0.78f ? b : pick < 0.9f ? new Color("a8c45a") : new Color("b98d58");
                var dir = Noise.Hash01(cx, cz, 5) < 0.5f ? wx : wz;
                var furrow = 0.5f + (0.5f * Mathf.Sin(dir * 34f));
                var h = 0.025f + (furrow * 0.008f) + (n * 0.015f);
                return (Rim(h, landRim, interior), crop.Darkened(furrow * 0.12f));
            }
            case Terrain.Village:
            {
                var h = 0.03f + (n * 0.02f);
                var c = a.Lerp(b, fine);
                // Dirt lanes wander between the houses.
                var lane = Mathf.Abs(Noise.Value(wx * 2.4f, wz * 2.4f, 31) - 0.5f);
                c = c.Lerp(new Color("d6c49a"), (1f - Mathf.SmoothStep(0.02f, 0.05f, lane)) * 0.85f);
                return (Rim(h, landRim, interior), c);
            }
            case Terrain.Sand:
            {
                var dunes = 0.5f + (0.5f * Mathf.Sin((wx * 4.5f) + (wz * 2.0f) + (n * 4f)));
                var h = 0.02f + (dunes * 0.05f * interior) + (n * 0.02f);
                return (Rim(h, landRim, interior), a.Lerp(b, 1f - dunes));
            }
            case Terrain.Water:
            {
                // Purely world-space so lakes spanning several tiles show no trace of the hex grid.
                var bed = WaterRim - 0.02f + (n * 0.04f);
                return (bed, a.Lerp(b, Mathf.SmoothStep(0.25f, 0.75f, n)));
            }
            case Terrain.Mountain:
            {
                var ridge = Noise.Ridge(wx * 1.15f, wz * 1.15f, 3, 17);
                var mass = Mathf.SmoothStep(0.06f, 0.5f, edge);
                var h = 0.05f + (mass * (0.18f + (0.95f * ridge)));
                var rock = a.Lerp(b, fine);
                var c = h < 0.14f ? _style.GroundA(Terrain.Grass).Lerp(rock, Mathf.SmoothStep(0.06f, 0.14f, h)) : rock;
                c = c.Lerp(Palette.Snow, Mathf.SmoothStep(_style.SnowLine, _style.SnowLine + 0.15f, h + ((fine - 0.5f) * 0.08f)));
                return (Rim(h, landRim, interior), c);
            }
            case Terrain.Snow:
            {
                var drift = Noise.Fbm(wx * 2.2f + 9f, wz * 2.2f, 2, 13);
                var h = 0.045f + (n * 0.05f) + (drift * 0.04f * interior);
                var c = a.Lerp(b, Mathf.SmoothStep(0.3f, 0.8f, drift)).Darkened((fine - 0.5f) * 0.03f);
                return (Rim(h, landRim, interior), c);
            }
            case Terrain.Swamp:
            {
                var puddle = Mathf.SmoothStep(0.4f, 0.56f, Noise.Fbm(wx * 3.4f, wz * 3.4f - 4f, 2, 57));
                var h = 0.0f + (n * 0.03f) - (puddle * 0.13f * interior);
                var c = a.Lerp(b, fine).Lerp(new Color("4a5a36"), puddle * 0.6f);
                return (Rim(h, landRim, interior), c);
            }
            case Terrain.Orchard:
            {
                // Mown stripes between the tree rows.
                var stripe = 0.5f + (0.5f * Mathf.Sin(wx * 11f));
                var h = 0.03f + (n * 0.035f);
                return (Rim(h, landRim, interior), a.Lerp(b, stripe * 0.8f).Darkened((fine - 0.5f) * 0.05f));
            }
            default:
                return (landRim, Colors.Magenta);
        }
    }

    /// <summary>1 on the raised banks between rice paddies, 0 in the flooded cells.</summary>
    private static float PaddyBund(float wx, float wz)
    {
        const float cell = 0.42f;
        var jx = wx + (Noise.Value(wx * 1.3f, wz * 1.3f, 61) * 0.12f);
        var jz = wz + (Noise.Value(wx * 1.3f + 4f, wz * 1.3f, 61) * 0.12f);
        var fx = (jx / cell) - Mathf.Floor(jx / cell);
        var fz = (jz / cell) - Mathf.Floor(jz / cell);
        var d = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fz, 1f - fz)) * cell;
        return 1f - Mathf.SmoothStep(0.016f, 0.03f, d);
    }

    private static float Rim(float interiorHeight, float rimHeight, float interior) =>
        rimHeight + ((interiorHeight - rimHeight) * interior);
}
