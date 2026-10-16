using System;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>Builds the ground (top surface + earthy skirt) and lake surface meshes for one tile.</summary>
public static class TileMeshBuilder
{
    public const int Subdivisions = 9;
    public const float TopScale = 0.988f;
    public const float SkirtBottom = -0.85f;

    public static ArrayMesh BuildGround(TerrainSampler sampler)
    {
        var kit = new MeshKit();
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

            int Index(int i, int j) => (i * (i + 1) / 2) + j;

            for (var i = 0; i <= n; i++)
            {
                for (var j = 0; j <= i; j++)
                {
                    var p = (ka * (i / (float)n)) + ((kb - ka) * (j / (float)n));
                    var s = sampler.Evaluate(p.X, p.Z);
                    var hx0 = sampler.Evaluate(p.X - eps, p.Z).Height;
                    var hx1 = sampler.Evaluate(p.X + eps, p.Z).Height;
                    var hz0 = sampler.Evaluate(p.X, p.Z - eps).Height;
                    var hz1 = sampler.Evaluate(p.X, p.Z + eps).Height;
                    var idx = Index(i, j);
                    pos[idx] = new Vector3(p.X, s.Height, p.Z);
                    nor[idx] = new Vector3((hx0 - hx1) / (2f * eps), 1f, (hz0 - hz1) / (2f * eps)).Normalized();
                    col[idx] = s.Color;
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
                    if (j < i)
                    {
                        var d = Index(i, j + 1);
                        kit.TriangleSmooth(pos[a], pos[c], pos[d], nor[a], nor[c], nor[d], col[a], col[c], col[d]);
                    }
                }
            }

            BuildSkirtEdge(kit, sampler, k, ka, kb);
        }

        return kit.Commit(withCustom: false);
    }

    /// <summary>Earthy cliff under edge <paramref name="edge"/>: grassy lip, soil band, then rock fading into the sea.</summary>
    private static void BuildSkirtEdge(MeshKit kit, TerrainSampler sampler, int edge, Vector3 ka, Vector3 kb)
    {
        const int segments = Subdivisions;
        var outward = HexLayout.EdgeDirection(edge);
        float[] depths = [0f, -0.06f, -0.2f, -0.45f, SkirtBottom];
        float[] insets = [1f, 0.995f, 0.985f, 0.97f, 0.94f];

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
                    0 => lip.Darkened(0.18f),
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

    /// <summary>Flat lake surface over every part of the tile touched by water; the shader handles depth and foam.</summary>
    public static ArrayMesh? BuildWater(TerrainSampler sampler, TileSpec tile)
    {
        if (!tile.ToArray().Contains(Terrain.Water))
            return null;

        var kit = new MeshKit();
        const int n = Subdivisions;
        var weights = new float[7];
        var up = Vector3.Up;
        var color = new Color(1, 1, 1);

        for (var k = 0; k < 6; k++)
        {
            var ka = HexLayout.Corner(k - 1 + 6, HexLayout.Radius * TopScale);
            var kb = HexLayout.Corner(k, HexLayout.Radius * TopScale);

            Vector3 P(int i, int j) => (ka * (i / (float)n)) + ((kb - ka) * (j / (float)n));

            bool Wet(Vector3 p)
            {
                sampler.TerrainWeights(p.X, p.Z, weights);
                return weights[(int)Terrain.Water] > 0.01f;
            }

            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j <= i; j++)
                {
                    var a = P(i, j);
                    var b = P(i + 1, j);
                    var c = P(i + 1, j + 1);
                    if (Wet(a) || Wet(b) || Wet(c))
                        kit.TriangleSmooth(Lift(a), Lift(b), Lift(c), up, up, up, color, color, color);
                    if (j < i)
                    {
                        var d = P(i, j + 1);
                        if (Wet(a) || Wet(c) || Wet(d))
                            kit.TriangleSmooth(Lift(a), Lift(c), Lift(d), up, up, up, color, color, color);
                    }
                }
            }
        }

        return kit.IsEmpty ? null : kit.Commit(withCustom: false);

        static Vector3 Lift(Vector3 p) => new(p.X, TerrainSampler.WaterLevel, p.Z);
    }
}
