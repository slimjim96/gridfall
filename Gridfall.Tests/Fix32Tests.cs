using Gridfall.Core.Math;
using Xunit;

namespace Gridfall.Tests;

public class Fix32Tests
{
    [Fact]
    public void One_IsExactlySixtyFiveThousandFiveHundredThirtySixRaw()
    {
        Assert.Equal(65536, Fix32.One.Raw);
        Assert.Equal(1, Fix32.One.ToInt());
    }

    [Fact]
    public void FromFraction_ProducesTheNearestRepresentableValue()
    {
        Fix32 third = Fix32.FromFraction(1, 3);
        Assert.Equal(21845, third.Raw);            // floor(65536/3)
        Assert.Equal(0, third.ToInt());
        Assert.Equal(Fix32.FromRaw(32768), Fix32.FromFraction(1, 2));
    }

    [Fact]
    public void AddAndSubtract_AreExact()
    {
        Fix32 a = Fix32.FromFraction(1, 3);
        Fix32 sum = a + a + a;
        Assert.Equal(65535, sum.Raw);              // three thirds, one raw unit short
        Assert.Equal(Fix32.Zero, a - a);
    }

    [Theory]
    [InlineData(3, 4, 12)]
    [InlineData(-3, 4, -12)]
    [InlineData(-3, -4, 12)]
    public void Multiply_IsExactForIntegers(int a, int b, int expected)
        => Assert.Equal(Fix32.FromInt(expected), Fix32.FromInt(a) * Fix32.FromInt(b));

    [Fact]
    public void MultiplyAndDivide_TruncateTowardZero_Symmetrically()
    {
        Fix32 seven = Fix32.FromInt(7);
        Fix32 two = Fix32.FromInt(2);

        Assert.Equal(3, (seven / two).ToInt());
        Assert.Equal(-3, ((-seven) / two).ToInt());

        // The asymmetric alternative -- arithmetic shift -- would floor to -4.
        Assert.NotEqual(-4, ((-seven) / two).ToInt());
    }

    [Fact]
    public void ToInt_TruncatesTowardZero()
    {
        Assert.Equal(1, Fix32.FromFraction(19, 10).ToInt());
        Assert.Equal(-1, Fix32.FromFraction(-19, 10).ToInt());
        Assert.Equal(-2, Fix32.FromFraction(-19, 10).FloorToInt());
    }

    [Fact]
    public void Sqrt_IsExactForPerfectSquares()
    {
        for (int i = 0; i <= 100; i++)
            Assert.Equal(Fix32.FromInt(i), FixMath.Sqrt(Fix32.FromInt(i * i)));
    }

    [Fact]
    public void Sqrt_IsTheExactIntegerFloorOfTheTrueRoot()
    {
        // Asserted on raw values in exact long arithmetic, NOT with Fix32
        // multiply: that operator truncates, so r*r <= v can hold for r+1 as
        // well, and the assertion would be weaker than it looks.
        for (int i = 1; i <= 5000; i++)
        {
            Fix32 v = Fix32.FromFraction(i, 7);
            Fix32 r = FixMath.Sqrt(v);

            long target = (long)v.Raw << Fix32.FractionalBits;
            long lo = (long)r.Raw * r.Raw;
            long hi = (long)(r.Raw + 1) * (r.Raw + 1);

            Assert.True(lo <= target, $"sqrt({v}) = {r} overshoots");
            Assert.True(hi > target, $"sqrt({v}) = {r} is not the largest such value");
        }
    }

    [Fact]
    public void Sqrt_OfZeroAndNegative_IsZero()
    {
        Assert.Equal(Fix32.Zero, FixMath.Sqrt(Fix32.Zero));
        Assert.Equal(Fix32.Zero, FixMath.Sqrt(Fix32.FromInt(-4)));
    }

    private static int ApplyWithAccumulator(Fix32 perTick, int ticks)
    {
        int applied = 0;
        Fix32 accumulator = Fix32.Zero;
        for (int t = 0; t < ticks; t++)
        {
            accumulator += perTick;
            if (accumulator < Fix32.One) continue;
            int whole = accumulator.ToInt();
            applied += whole;
            accumulator -= Fix32.FromInt(whole);
        }
        return applied;
    }

    [Fact]
    public void SubUnitPerTickValues_TruncateToNothingWithoutAnAccumulator()
    {
        // The classic fixed-point mistake: a value below 1 truncates to zero every
        // tick, so the effect deals nothing at all, forever. Engine guide 03.
        Fix32 perTick = Fix32.FromFraction(1, 64);

        int naive = 0;
        for (int t = 0; t < 64; t++) naive += perTick.ToInt();
        Assert.Equal(0, naive);

        Assert.Equal(1, ApplyWithAccumulator(perTick, 64));
    }

    [Fact]
    public void AnInexactRate_LosesTheRemainder_AndThatIsTheDesignedBehaviour()
    {
        // 1/100 is NOT representable: FromFraction(1,100) truncates to 655/65536,
        // slightly under 0.01. Accumulated 100 times that is 0.99945 -- so the
        // effect lands on tick 101, not tick 100.
        //
        // This is real and permanent, not a bug to fix. Content authors picking
        // rates should prefer powers of two where the exact tick matters.
        Fix32 perTick = Fix32.FromFraction(1, 100);
        Assert.Equal(655, perTick.Raw);

        Assert.Equal(0, ApplyWithAccumulator(perTick, 100));
        Assert.Equal(1, ApplyWithAccumulator(perTick, 101));
    }

    [Fact]
    public void DistanceSquared_AvoidsSqrtAndStaysExact()
    {
        var a = new FixVec2(Fix32.FromInt(0), Fix32.FromInt(0));
        var b = new FixVec2(Fix32.FromInt(3), Fix32.FromInt(4));
        Assert.Equal(Fix32.FromInt(25), FixVec2.DistanceSquared(a, b));
        Assert.Equal(Fix32.FromInt(5), (b - a).Length());
    }

    [Fact]
    public void ToString_RendersExactly()
    {
        Assert.Equal("1.00000", Fix32.One.ToString());
        Assert.Equal("0.50000", Fix32.Half.ToString());
        Assert.Equal("-2.25000", Fix32.FromFraction(-9, 4).ToString());
    }
}
