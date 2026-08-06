namespace Gridfall.Core.Math;

/// <summary>
/// Deterministic math for Fix32. Nothing here calls System.Math -- platform
/// math libraries are not bit-specified and that is exactly what Core avoids.
/// </summary>
public static class FixMath
{
    /// <summary>
    /// Exact integer square root, bit by bit. No iteration count to tune and no
    /// approximation error: for any input it returns the largest r with
    /// r*r &lt;= value, in Q16.16. Identical on every platform by construction.
    /// </summary>
    public static Fix32 Sqrt(Fix32 value)
    {
        if (value.Raw <= 0) return Fix32.Zero;

        // sqrt of a Q16.16 value v is sqrt(raw << 16) in raw units.
        ulong n = (ulong)(uint)value.Raw << Fix32.FractionalBits;

        ulong result = 0;
        ulong bit = 1UL << 62;
        while (bit > n) bit >>= 2;

        while (bit != 0)
        {
            if (n >= result + bit)
            {
                n -= result + bit;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }
            bit >>= 2;
        }

        return Fix32.FromRaw((int)result);
    }

    public static Fix32 Clamp(Fix32 value, Fix32 min, Fix32 max)
        => value < min ? min : value > max ? max : value;

    public static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    // Sin/Cos are deliberately absent. A lookup table has to be seeded from
    // somewhere, and seeding it with double at static init puts platform-
    // dependent values inside Core -- the one thing Core exists to prevent.
    // Nothing in the simulation needs trigonometry yet: movement is
    // four-directional and projectiles travel along normalized vectors.
    // When something does need it, generate the table with integer-only
    // CORDIC and add the determinism test alongside it.
}
