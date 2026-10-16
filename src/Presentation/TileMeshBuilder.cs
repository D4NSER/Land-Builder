using System;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>Builds the ground (top surface + earthy skirt) and lake surface meshes for one tile.</summary>
public static class TileMeshBuilder
{
    public const int Subdivisions = 9;
    /// <summary>Tops overlap their neighbours by a hair so floating-point cracks never show the sea through the land.</summary>
    public const float TopScale = 1.003f;
    public const float SkirtBottom = -0.62f;

    /// <summary>Builds the ground (top + skirt) and, where the tile has lakes, rivers or marsh puddles, a water surface.</summary>
    /// <param name="waterToWorld">
    /// When given, the water surface is emitted in world space and snapped, so lakes spanning several tiles weld into
    /// one seamless sheet (transparent surfaces would otherwise show hairline cracks or double-blended seams).
    /// </param>
    public static (ArrayMesh Ground, ArrayMesh? Water) Build(TerrainSampler sampler, Transform3D? waterToWorld = null)
    {
        var kit = new MeshKit();
        var waterKit = new MeshKit();
        if (waterToWorld is Transform3D worldTransform)
        {
            waterKit.Transform = worldTransform;
            waterKit.SnapGrid = 0.0005f;
        }
        var up = Vector3.Up;
        const int n = Subdivisions;
        const float eps = 0.012f;

        for (var k = 0; k < 6; k++)
        {
            var ka = HexLayout.Corner(k - 1 + 6, HexLayout.Radius * TopScale);
            var kb = HexLayout.Corner(k, HexLayout.Radius * TopScale);
            var count = (n + 1) * (n + 2) / 2;
            var pos = new Vector3[count];
            var nor = new Vector3[count];
            var col = new Color[count];
            var wet = new bool[count];
            var clarity = new Color[count];

            int Index(int i, int j) => (i * (i + 1) / 2) + j;

            for (var i = 0; i <= n; i++)
            {
                for (var j = 0; j <= i; j++)
                {
                    var p = (ka * (i / (float)n)) + ((kb - ka) * (j / (float)n));
                    var s = sampler.Evaluate(p.X, p.Z);
                    // Forward differences: two extra samples per vertex instead of four.
                    var hx1 = sampler.Evaluate(p.X + eps, p.Z).Height;
                    var hz1 = sampler.Evaluate(p.X, p.Z + eps).Height;
                    var idx = Index(i, j);
                    pos[idx] = new Vector3(p.X, s.Height, p.Z);
                    nor[idx] = new Vector3((s.Height - hx1) / eps, 1f, (s.Height - hz1) / eps).Normalized();
                    col[idx] = s.Color;
                    wet[idx] = s.Wet > 0.01f;
                    // Vertex colour carries clarity (white = clear) so the water shader can muddy marsh puddles.
                    var clear = 1f - s.Murk;
                    clarity[idx] = new Color(clear, clear, clear);
                }
            }

            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j <= i; j++)
                {
                    var a = Index(i, j);
                    var b = Index(i + 1, j);
                    var c = Index(i + 1, j + 1);
                    kit.TriangleSmooth(pos[a], pos[b], pos[c], nor[a], nor[b], nor[c], col[a], col[b], col[c]);
                    if (wet[a] || wet[b] || wet[c])
                        waterKit.TriangleSmooth(Lift(pos[a]), Lift(pos[b]), Lift(pos[c]), up, up, up, clarity[a], clarity[b], clarity[c]);
                    if (j < i)
                    {
                        var d = Index(i, j + 1);
                        kit.TriangleSmooth(pos[a], pos[c], pos[d], nor[a], nor[c], nor[d], col[a], col[c], col[d]);
                        if (wet[a] || wet[c] || wet[d])
                            waterKit.TriangleSmooth(Lift(pos[a]), Lift(pos[c]), Lift(pos[d]), up, up, up, clarity[a], clarity[c], clarity[d]);
                    }
                }
            }

            BuildSkirtEdge(kit, sampler, k, ka, kb);
        }

        return (kit.Commit(withCustom: false), waterKit.IsEmpty ? null : waterKit.Commit(withCustom: false));

        static Vector3 Lift(Vector3 p) => new(p.X / TopScale, TerrainSampler.WaterLevel, p.Z / TopScale);
    }

    /// <summary>Earthy cliff under edge <paramref name="edge"/>: grassy lip, soil band, then rock fading into the sea.</summary>
    private static void BuildSkirtEdge(MeshKit kit, TerrainSampler sampler, int edge, Vector3 ka, Vector3 kb)
    {
        const int segments = Subdivisions;
        var outward = HexLayout.EdgeDirection(edge);
        float[] depths = [0f, -0.06f, -0.18f, -0.38f, SkirtBottom];
        // The upper bands stay vertical so neighbouring tiles meet with no groove; only the rock base tapers.
        float[] insets = [1f, 1f, 1f, 0.985f, 0.95f];

        for (var s = 0; s < segments; s++)
        {
            var t0 = s / (float)segments;
            var t1 = (s + 1) / (float)segments;
            var p0 = ka.Lerp(kb, t0);
            var p1 = ka.Lerp(kb, t1);
            var s0 = sampler.Evaluate(p0.X, p0.Z);
            var s1 = sampler.Evaluate(p1.X, p1.Z);

            for (var band = 0; band < depths.Length - 1; band++)
            {
                var yTop0 = band == 0 ? s0.Height : s0.Height + depths[band];
                var yTop1 = band == 0 ? s1.Height : s1.Height + depths[band];
                var yBot0 = band + 1 == depths.Length - 1 ? SkirtBottom : s0.Height + depths[band + 1];
                var yBot1 = band + 1 == depths.Length - 1 ? SkirtBottom : s1.Height + depths[band + 1];
                var top0 = new Vector3(p0.X * insets[band], yTop0, p0.Z * insets[band]);
                var top1 = new Vector3(p1.X * insets[band], yTop1, p1.Z * insets[band]);
                var bot0 = new Vector3(p0.X * insets[band + 1], yBot0, p0.Z * insets[band + 1]);
                var bot1 = new Vector3(p1.X * insets[band + 1], yBot1, p1.Z * insets[band + 1]);

                var lip = s0.Color.Lerp(s1.Color, 0.5f);
                var color = band switch
                {
                    0 => lip.Darkened(0.1f),
                    1 => Palette.SoilTop.Lerp(lip, 0.15f),
                    2 => Palette.SoilMid,
                    _ => Palette.Rock
                };
                if (band == 2 && ((edge + s) % 3 == 0))
                    color = color.Darkened(0.06f);

                // Outward-facing quad: counter-clockwise seen from outside.
                var n = outward;
                kit.TriangleSmooth(bot0, top1, top0, n, n, n, color, color, color);
                kit.TriangleSmooth(bot0, bot1, top1, n, n, n, color, color, color);
            }
        }
    }
}
