namespace Gridfall.Core;

/// <summary>
/// FNV-1a, 64-bit. Not cryptographic -- it needs to be fast and stable, not secure.
/// Stability is the requirement: the same bytes must produce the same hash on every
/// platform forever, because recorded traces store these values.
/// </summary>
public static class FnvHash
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Init() => Offset;

    public static ulong Combine(ulong hash, ulong value)
    {
        for (int i = 0; i < 8; i++)
        {
            hash ^= (byte)(value >> (i * 8));
            hash *= Prime;
        }
        return hash;
    }

    public static ulong Combine(ulong hash, int value) => Combine(hash, (ulong)(uint)value);

    public static ulong Combine(ulong hash, int a, int b) => Combine(Combine(hash, a), b);

    public static ulong Combine(ulong hash, int a, int b, int c) => Combine(Combine(Combine(hash, a), b), c);

    public static ulong CombineBytes(ulong hash, ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= Prime;
        }
        return hash;
    }

    public static ulong CombineInts(ulong hash, ReadOnlySpan<int> values)
    {
        foreach (int v in values) hash = Combine(hash, v);
        return hash;
    }

    public static ulong CombineBytes(ulong hash, ReadOnlySpan<sbyte> values)
    {
        foreach (sbyte v in values) { hash ^= (byte)v; hash *= Prime; }
        return hash;
    }
}
