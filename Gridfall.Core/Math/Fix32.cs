namespace Gridfall.Core.Math;

/// <summary>
/// Q16.16 fixed-point. All simulation arithmetic. See ADR-0002 and engine guide 03.
///
/// Backed by an int: 16 integer bits, 16 fractional bits. Range +/-32767.99998,
/// resolution 1/65536. Integer arithmetic, so identical on every platform and
/// runtime -- which is the entire reason this type exists instead of float.
/// </summary>
public readonly struct Fix32 : IEquatable<Fix32>, IComparable<Fix32>
{
    public const int FractionalBits = 16;
    public const int OneRaw = 1 << FractionalBits;

    public readonly int Raw;

    private Fix32(int raw) => Raw = raw;

    public static Fix32 FromRaw(int raw) => new(raw);

    public static Fix32 FromInt(int value) => new(value * OneRaw);

    /// <summary>
    /// The only way a decimal becomes a sim value. There is deliberately no
    /// FromFloat: that is the door every determinism bug walks through.
    /// </summary>
    public static Fix32 FromFraction(int numerator, int denominator)
    {
        if (denominator == 0) throw new DivideByZeroException("Fix32.FromFraction denominator is zero");
        return new Fix32((int)(((long)numerator * OneRaw) / denominator));
    }

    public static readonly Fix32 Zero = new(0);
    public static readonly Fix32 One = new(OneRaw);
    public static readonly Fix32 Half = new(OneRaw / 2);
    public static readonly Fix32 MaxValue = new(int.MaxValue);
    public static readonly Fix32 MinValue = new(int.MinValue);

    /// <summary>Truncates toward zero, matching the documented behaviour of * and /.</summary>
    public int ToInt() => Raw >= 0
        ? Raw >> FractionalBits
        : -(int)((-(long)Raw) >> FractionalBits);

    public int FloorToInt() => Raw >> FractionalBits;

    public int RoundToInt() => (Raw + (OneRaw / 2)) >> FractionalBits;

    /// <summary>The fractional part, always in [0,1). View layer and accumulators use this.</summary>
    public Fix32 Fraction() => new(Raw & (OneRaw - 1));

    // ---- arithmetic -------------------------------------------------------
    // Add and subtract are exact. Multiply and divide truncate toward zero,
    // consistently, on every platform. That consistency is the whole point.

    public static Fix32 operator +(Fix32 a, Fix32 b) => new(a.Raw + b.Raw);
    public static Fix32 operator -(Fix32 a, Fix32 b) => new(a.Raw - b.Raw);
    public static Fix32 operator -(Fix32 a) => new(-a.Raw);

    public static Fix32 operator *(Fix32 a, Fix32 b)
    {
        long p = (long)a.Raw * b.Raw;
        long r = p >= 0 ? (p >> FractionalBits) : -((-p) >> FractionalBits);
        return new Fix32(ToIntChecked(r));
    }

    public static Fix32 operator *(Fix32 a, int b) => new(ToIntChecked((long)a.Raw * b));

    public static Fix32 operator /(Fix32 a, Fix32 b)
    {
        if (b.Raw == 0) throw new DivideByZeroException("Fix32 division by zero");
        // C# integer division already truncates toward zero.
        return new Fix32(ToIntChecked(((long)a.Raw << FractionalBits) / b.Raw));
    }

    public static Fix32 operator /(Fix32 a, int b)
    {
        if (b == 0) throw new DivideByZeroException("Fix32 division by zero");
        return new Fix32((int)((long)a.Raw / b));
    }

    private static int ToIntChecked(long value)
    {
#if DEBUG
        if (value > int.MaxValue || value < int.MinValue)
            throw new OverflowException($"Fix32 overflow: raw result {value} does not fit in int");
#endif
        return unchecked((int)value);
    }

    // ---- comparison -------------------------------------------------------

    public static bool operator ==(Fix32 a, Fix32 b) => a.Raw == b.Raw;
    public static bool operator !=(Fix32 a, Fix32 b) => a.Raw != b.Raw;
    public static bool operator <(Fix32 a, Fix32 b) => a.Raw < b.Raw;
    public static bool operator >(Fix32 a, Fix32 b) => a.Raw > b.Raw;
    public static bool operator <=(Fix32 a, Fix32 b) => a.Raw <= b.Raw;
    public static bool operator >=(Fix32 a, Fix32 b) => a.Raw >= b.Raw;

    public bool Equals(Fix32 other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is Fix32 f && Raw == f.Raw;
    public override int GetHashCode() => Raw;
    public int CompareTo(Fix32 other) => Raw.CompareTo(other.Raw);

    public static Fix32 Min(Fix32 a, Fix32 b) => a.Raw <= b.Raw ? a : b;
    public static Fix32 Max(Fix32 a, Fix32 b) => a.Raw >= b.Raw ? a : b;
    public static Fix32 Abs(Fix32 a) => a.Raw >= 0 ? a : new Fix32(-a.Raw);

    /// <summary>
    /// VIEW LAYER ONLY. Calling this inside Gridfall.Core is a bug, and
    /// SourcePurityTests fails the build if it appears here.
    /// </summary>
    public float ToFloat() => Raw / (float)OneRaw;

    public override string ToString()
    {
        // Exact decimal rendering of a Q16.16 value, integer-only.
        bool neg = Raw < 0;
        long raw = neg ? -(long)Raw : Raw;
        long whole = raw >> FractionalBits;
        long frac = raw & (OneRaw - 1);
        // 5 decimal places is enough to distinguish every representable value.
        long scaled = (frac * 100000 + (OneRaw / 2)) >> FractionalBits;
        return $"{(neg ? "-" : "")}{whole}.{scaled:D5}";
    }
}
