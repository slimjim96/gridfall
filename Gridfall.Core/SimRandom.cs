namespace Gridfall.Core;

/// <summary>
/// The only randomness Core may use. Seeded, advanced only inside the tick loop,
/// and its position is part of the state hash -- so two runs that consumed a
/// different number of random values hash differently even if nothing else moved.
///
/// xorshift64*: small, fast, deterministic, and no dependency on System.Random
/// (whose algorithm is not contractually stable across runtimes).
/// </summary>
public sealed class SimRandom
{
    private ulong _state;
    private uint _draws;

    public SimRandom(uint seed)
    {
        // Zero is a fixed point of xorshift; splitmix the seed so seed 0 works.
        _state = SplitMix64(seed == 0 ? 0x9E3779B97F4A7C15UL : seed);
        if (_state == 0) _state = 0x9E3779B97F4A7C15UL;
        _draws = 0;
    }

    private SimRandom(ulong state, uint draws) { _state = state; _draws = draws; }

    /// <summary>Number of values drawn. Hashed -- see SimState.Hash.</summary>
    public uint Draws => _draws;

    public ulong RawState => _state;

    public ulong NextUInt64()
    {
        _draws++;
        ulong x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    public uint NextUInt32() => (uint)(NextUInt64() >> 32);

    /// <summary>Uniform in [0, exclusiveMax). Rejection-sampled, so no modulo bias.</summary>
    public int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        uint bound = (uint)exclusiveMax;
        uint threshold = (uint)(-(int)bound) % bound;
        while (true)
        {
            uint r = NextUInt32();
            if (r >= threshold) return (int)(r % bound);
        }
    }

    public SimRandom Clone() => new(_state, _draws);

    public void CopyFrom(SimRandom other) { _state = other._state; _draws = other._draws; }

    private static ulong SplitMix64(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
