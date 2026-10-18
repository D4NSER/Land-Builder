using Godot;

namespace LandBuilder.Presentation;

/// <summary>Small deterministic value-noise helpers evaluated in world space so neighbouring tiles blend seamlessly.</summary>
public static class Noise
{
    public static float Hash(int x, int y, int seed = 0)
    {
        unchecked
        {
            var h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    public static float Hash01(int a, int b = 0, int c = 0) => Hash(a * 92821 + c, b * 31337 - c, c);

    public static float Value(float x, float y, int seed = 0)
    {
        var xi = Mathf.FloorToInt(x);
        var yi = Mathf.FloorToInt(y);
        var tx = x - xi;
        var ty = y - yi;
        tx = tx * tx * (3f - (2f * tx));
        ty = ty * ty * (3f - (2f * ty));
        var a = Hash(xi, yi, seed);
        var b = Hash(xi + 1, yi, seed);
        var c = Hash(xi, yi + 1, seed);
        var d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    /// <summary>Fractal value noise in [0, 1].</summary>
    public static float Fbm(float x, float y, int octaves = 3, int seed = 0)
    {
        var sum = 0f;
        var amp = 0.5f;
        var norm = 0f;
        for (var i = 0; i < octaves; i++)
        {
            sum += Value(x, y, seed + (i * 17)) * amp;
            norm += amp;
            x = (x * 2.03f) + 11.7f;
            y = (y * 2.03f) - 5.3f;
            amp *= 0.5f;
        }

        return sum / norm;
    }

    /// <summary>Ridged noise for mountain crests, in [0, 1].</summary>
    public static float Ridge(float x, float y, int octaves = 3, int seed = 0)
    {
        var sum = 0f;
        var amp = 0.6f;
        var norm = 0f;
        for (var i = 0; i < octaves; i++)
        {
            var n = 1f - Mathf.Abs((Value(x, y, seed + (i * 31)) * 2f) - 1f);
            sum += n * n * amp;
            norm += amp;
            x *= 2.1f;
            y *= 2.1f;
            amp *= 0.5f;
        }

        return sum / norm;
    }
}
