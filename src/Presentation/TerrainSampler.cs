using System;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Evaluates height and colour across one tile. Sector terrains are blended with soft wedges so borders look
/// organic, and all noise is sampled in world space so neighbouring tiles continue each other seamlessly.
/// Heights at the hex rim are pinned per terrain (land vs water) so matched edges meet with no seam.
/// </summary>
public sealed class TerrainSampler
{
    public const float WaterLevel = -0.045f;
    public const float LandRim = 0.02f;
    public const float WaterRim = -0.085f;
    private const float BlendWidth = 0.13f;

    private readonly Terrain[] _sectors;
    private readonly Transform3D _toWorld;

    public TerrainSampler(TileSpec tile, Transform3D toWorld)
    {
        _sectors = tile.ToArray();
        _toWorld = toWorld;
    }

    public Terrain SectorTerrain(int sector) => _sectors[((sector % 6) + 6) % 6];

    public readonly struct Sample
    {
        public readonly float Height;
        public readonly Color Color;
        public readonly float Water;
        public readonly float Mountain;

        public Sample(float height, Color color, float water, float mountain)
        {
            Height = height;
            Color = color;
            Water = water;
            Mountain = mountain;
        }
    }

    /// <summary>Weight of each terrain at a local point (indexed by <see cref="Terrain"/>), summing to 1.</summary>
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
            weights[(int)_sectors[k]] += w;
            total += w;
        }

        if (total <= 0.0001f)
        {
            weights[(int)_sectors[0]] = 1f;
            return;
        }

        for (var i = 0; i < weights.Length; i++)
            weights[i] /= total;
    }

    public Vector3 ToWorld(float x, float y, float z) => _toWorld * new Vector3(x, y, z);

    public Sample Evaluate(float x, float z)
    {
        Span<float> w = stackalloc float[7];
        TerrainWeights(x, z, w);
        var world = _toWorld * new Vector3(x, 0, z);
        var wx = world.X;
        var wz = world.Z;
        var edge = HexLayout.DistanceToHexEdge(x, z);
        var interior = Mathf.SmoothStep(0.0f, 0.3f, edge);

        var height = 0f;
        var r = 0f;
        var g = 0f;
        var b = 0f;
        for (var i = 0; i < 7; i++)
        {
            if (w[i] < 0.0005f)
                continue;
            var terrain = (Terrain)i;
            var (h, c) = TerrainHeightColor(terrain, wx, wz, interior, edge);
            height += h * w[i];
            r += c.R * w[i];
            g += c.G * w[i];
            b += c.B * w[i];
        }

        var color = new Color(r, g, b);
        var water = w[(int)Terrain.Water];
        var mountain = w[(int)Terrain.Mountain];

        // Sandy shoreline where land dips to the water line.
        if (water > 0.02f && water < 0.98f)
        {
            var shore = 1f - Mathf.Clamp(Mathf.Abs(height - WaterLevel) / 0.05f, 0f, 1f);
            color = color.Lerp(new Color("e3d29d"), shore * 0.75f);
        }

        return new Sample(height, color, water, mountain);
    }

    private static (float Height, Color Color) TerrainHeightColor(Terrain terrain, float wx, float wz, float interior, float edge)
    {
        var n = Noise.Fbm(wx * 1.6f, wz * 1.6f, 3, 3);
        var fine = Noise.Value(wx * 7.0f, wz * 7.0f, 11);
        switch (terrain)
        {
            case Terrain.Grass:
            {
                var h = 0.03f + (n * 0.05f);
                var patch = Noise.Fbm(wx * 0.9f + 40f, wz * 0.9f, 2, 5);
                var c = new Color("74b956").Lerp(new Color("9cc95c"), Mathf.SmoothStep(0.45f, 0.75f, patch));
                c = c.Darkened((fine - 0.5f) * 0.08f);
                return (Rim(h, LandRim, interior), c);
            }
            case Terrain.Forest:
            {
                var h = 0.035f + (n * 0.06f);
                var c = new Color("4a8a43").Lerp(new Color("3a733a"), n);
                c = c.Darkened((fine - 0.5f) * 0.1f);
                return (Rim(h, LandRim, interior), c);
            }
            case Terrain.Field:
            {
                // Patchwork of crops: each world cell gets its own crop colour and furrow direction.
                var cx = Mathf.FloorToInt((wx * 1.25f) + (Noise.Value(wx * 2f, wz * 2f, 9) * 0.6f));
                var cz = Mathf.FloorToInt((wz * 1.25f) + (Noise.Value(wx * 2f + 7f, wz * 2f, 9) * 0.6f));
                var pick = Noise.Hash01(cx, cz, 21);
                var crop = pick < 0.55f ? new Color("ecc65a") : pick < 0.78f ? new Color("d9a843") : pick < 0.9f ? new Color("a8c45a") : new Color("b98d58");
                var dir = Noise.Hash01(cx, cz, 5) < 0.5f ? wx : wz;
                var furrow = 0.5f + (0.5f * Mathf.Sin(dir * 34f));
                var c = crop.Darkened(furrow * 0.12f);
                var h = 0.025f + (furrow * 0.008f) + (n * 0.015f);
                return (Rim(h, LandRim, interior), c);
            }
            case Terrain.Village:
            {
                var h = 0.03f + (n * 0.02f);
                var c = new Color("aecb7a").Lerp(new Color("c9cf8f"), fine);
                return (Rim(h, LandRim, interior), c);
            }
            case Terrain.Sand:
            {
                var dunes = 0.5f + (0.5f * Mathf.Sin((wx * 4.5f) + (wz * 2.0f) + (n * 4f)));
                var h = 0.02f + (dunes * 0.05f * interior) + (n * 0.02f);
                var c = new Color("efd79b").Lerp(new Color("e2c07e"), 1f - dunes);
                return (Rim(h, LandRim, interior), c);
            }
            case Terrain.Water:
            {
                var bed = -0.19f + (n * 0.04f);
                var c = new Color("8cb8a0").Lerp(new Color("5f8f8c"), interior);
                return (Rim(bed, WaterRim, interior), c);
            }
            case Terrain.Mountain:
            {
                var ridge = Noise.Ridge(wx * 1.15f, wz * 1.15f, 3, 17);
                var mass = Mathf.SmoothStep(0.06f, 0.5f, edge);
                var h = 0.05f + (mass * (0.18f + (0.95f * ridge)));
                var rock = new Color("9b968c").Lerp(new Color("7e7a74"), fine);
                var c = h < 0.14f ? new Color("8fb565").Lerp(rock, Mathf.SmoothStep(0.06f, 0.14f, h)) : rock;
                c = c.Lerp(Palette.Snow, Mathf.SmoothStep(0.6f, 0.75f, h + ((fine - 0.5f) * 0.08f)));
                return (Rim(h, LandRim, interior), c);
            }
            default:
                return (LandRim, Colors.Magenta);
        }
    }

    private static float Rim(float interiorHeight, float rimHeight, float interior) =>
        rimHeight + ((interiorHeight - rimHeight) * interior);

    /// <summary>Height at the rim for a given edge sector (used for the skirt and seams).</summary>
    public static float RimHeight(Terrain terrain) => terrain == Terrain.Water ? WaterRim : LandRim;
}
