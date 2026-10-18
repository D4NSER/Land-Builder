namespace LandBuilder.Domain;

/// <summary>Deterministic xorshift64* generator. The whole state is one ulong so it can be saved and replayed.</summary>
public struct Rng
{
    public ulong State;

    public Rng(ulong seed)
    {
        State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public ulong NextU64()
    {
        var x = State;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        State = x;
        return x * 2685821657736338717UL;
    }

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)((NextU64() >> 11) % (ulong)maxExclusive);
    }

    public int NextInt(int minInclusive, int maxExclusive) => minInclusive + NextInt(maxExclusive - minInclusive);

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextU64() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double probability) => NextDouble() < probability;

    public T PickWeighted<T>(IReadOnlyList<(T Item, double Weight)> options)
    {
        var total = 0.0;
        foreach (var option in options)
            total += Math.Max(0.0, option.Weight);

        if (total <= 0.0)
            return options[0].Item;

        var roll = NextDouble() * total;
        foreach (var option in options)
        {
            roll -= Math.Max(0.0, option.Weight);
            if (roll < 0.0)
                return option.Item;
        }

        return options[^1].Item;
    }
}
