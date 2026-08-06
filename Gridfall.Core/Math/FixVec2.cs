namespace Gridfall.Core.Math;

/// <summary>
/// A 2D vector in cell units. Sub-cell positions, velocities, offsets.
/// Never pixels -- the sim has no concept of screen space (engine guide 07).
/// </summary>
public readonly struct FixVec2 : IEquatable<FixVec2>
{
    public readonly Fix32 X;
    public readonly Fix32 Y;

    public FixVec2(Fix32 x, Fix32 y) { X = x; Y = y; }

    public static readonly FixVec2 Zero = new(Fix32.Zero, Fix32.Zero);

    public static FixVec2 operator +(FixVec2 a, FixVec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static FixVec2 operator -(FixVec2 a, FixVec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static FixVec2 operator -(FixVec2 a) => new(-a.X, -a.Y);
    public static FixVec2 operator *(FixVec2 a, Fix32 s) => new(a.X * s, a.Y * s);

    /// <summary>
    /// Prefer this over Length for every range and proximity test. A square root
    /// per creep per tower per tick buys nothing (engine guide 03).
    /// </summary>
    public Fix32 LengthSquared() => X * X + Y * Y;

    public Fix32 Length() => FixMath.Sqrt(LengthSquared());

    public static Fix32 DistanceSquared(FixVec2 a, FixVec2 b) => (a - b).LengthSquared();

    /// <summary>One sqrt and two divides. Expensive -- keep it out of hot loops.</summary>
    public FixVec2 Normalized()
    {
        Fix32 len = Length();
        if (len.Raw == 0) return Zero;
        return new FixVec2(X / len, Y / len);
    }

    public bool Equals(FixVec2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is FixVec2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X.Raw, Y.Raw);
    public static bool operator ==(FixVec2 a, FixVec2 b) => a.Equals(b);
    public static bool operator !=(FixVec2 a, FixVec2 b) => !a.Equals(b);

    public override string ToString() => $"({X}, {Y})";
}
