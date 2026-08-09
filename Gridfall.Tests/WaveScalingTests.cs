using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Per-wave HP scaling. Without it later waves cannot be harder -- more visitors
/// of a fixed toughness just hand the player more bounty, which becomes more
/// stations. See 2026-08-06-crossroads-12-waves-balance.md.
/// </summary>
public class WaveScalingTests
{
    private const string Visitors = """
    { "id": "runner", "name": "Runner", "appetite": 100, "speed": 0.06, "bounty": 8 }
    """;

    private static WaveDef[] Load(string wavesJson)
    {
        VisitorDef[] visitors = ContentLoader.LoadVisitors(new[] { ("runner.json", Visitors) });
        return ContentLoader.LoadWaves(wavesJson, visitors, "waves.json");
    }

    private const string ThreeWaves = """
    {
      "map": "t", "appetiteGrowth": 1.5,
      "waves": [
        { "index": 1, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 2, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 3, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] }
      ]
    }
    """;

    [Fact]
    public void WaveOne_IsNeverScaled()
        => Assert.Equal(Fix32.One, Load(ThreeWaves)[0].AppetiteScale);

    [Fact]
    public void GrowthCompoundsWaveToWave()
    {
        WaveDef[] waves = Load(ThreeWaves);
        Assert.Equal(Fix32.FromFraction(15, 10), waves[1].AppetiteScale);
        // 1.5^2, allowing for one raw unit of fixed-point truncation.
        Assert.InRange(waves[2].AppetiteScale.Raw, Fix32.FromFraction(225, 100).Raw - 2,
                                             Fix32.FromFraction(225, 100).Raw + 2);
    }

    [Fact]
    public void NoGrowthDeclared_MeansNoScaling()
    {
        const string flat = """
        { "map": "t", "waves": [
            { "index": 1, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
            { "index": 2, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] } ] }
        """;
        Assert.All(Load(flat), w => Assert.Equal(Fix32.One, w.AppetiteScale));
    }

    [Fact]
    public void AnExplicitScale_OverridesTheCurve()
    {
        const string json = """
        { "map": "t", "appetiteGrowth": 1.5, "waves": [
            { "index": 1, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
            { "index": 2, "appetiteScale": 10.0, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] } ] }
        """;
        Assert.Equal(Fix32.FromInt(10), Load(json)[1].AppetiteScale);
    }

    [Fact]
    public void ShrinkingGrowth_IsRejected()
    {
        const string json = """
        { "map": "t", "appetiteGrowth": 0.9, "waves": [
            { "index": 1, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] } ] }
        """;
        ContentException ex = Assert.Throws<ContentException>(() => Load(json));
        Assert.Contains("weaker", ex.Message);
    }

    [Fact]
    public void ScaledVisitors_ActuallySpawnWithMoreHealth()
    {
        // The end-to-end claim: the scalar reaches a live visitor.
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new StartWaveCommand());
        sim.Tick();

        int slot = sim.State.VisitorSlotByOrder(0);
        int wave1Appetite = sim.State.VisitorAppetite(slot);

        Assert.Equal(sim.Content.Visitor(sim.State.VisitorDefIndex(slot)).Appetite, wave1Appetite);
    }

    [Fact]
    public void ScalingDoesNotBreakDeterminism()
    {
        static ulong RunAndHash(uint seed)
        {
            Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed);
            sim.Enqueue(new StartWaveCommand());
            for (int t = 0; t < 400; t++) sim.Tick();
            return sim.Hash();
        }

        Assert.Equal(RunAndHash(9), RunAndHash(9));
    }

    // ---- appetiteGrowthFrom: where the ramp starts -------------------------------

    private static string SixWaves(string header) => $$"""
    {
      "map": "t", {{header}},
      "waves": [
        { "index": 1, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 2, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 3, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 4, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 5, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 6, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] }
      ]
    }
    """;

    [Fact]
    public void WithoutGrowthFrom_TheCurveIsExactlyWhatItWasBefore()
    {
        // The back-compat guarantee. gauntlet declares no appetiteGrowthFrom and its
        // numbers must not move because crossroads needed a different shape.
        WaveDef[] withoutField = Load(SixWaves("\"appetiteGrowth\": 1.5"));
        WaveDef[] withDefault = Load(SixWaves("\"appetiteGrowth\": 1.5, \"appetiteGrowthFrom\": 1"));

        for (int i = 0; i < withoutField.Length; i++)
            Assert.Equal(withoutField[i].AppetiteScale.Raw, withDefault[i].AppetiteScale.Raw);
    }

    [Fact]
    public void WavesAtOrBeforeGrowthFrom_AreFlat()
    {
        // The opening is where the player is broke and thin. Making it flat is
        // what lets the late rate be steep -- see early-economy-2.
        WaveDef[] waves = Load(SixWaves("\"appetiteGrowth\": 1.5, \"appetiteGrowthFrom\": 4"));

        Assert.Equal(Fix32.One, waves[0].AppetiteScale);   // wave 1
        Assert.Equal(Fix32.One, waves[1].AppetiteScale);   // wave 2
        Assert.Equal(Fix32.One, waves[2].AppetiteScale);   // wave 3
        Assert.Equal(Fix32.One, waves[3].AppetiteScale);   // wave 4 -- the last flat one
    }

    [Fact]
    public void AfterGrowthFrom_TheRampRunsAtTheDeclaredRate()
    {
        WaveDef[] waves = Load(SixWaves("\"appetiteGrowth\": 1.5, \"appetiteGrowthFrom\": 4"));

        Assert.Equal(Fix32.FromFraction(15, 10), waves[4].AppetiteScale);          // wave 5 = 1.5^1
        Assert.InRange(waves[5].AppetiteScale.Raw,                                  // wave 6 = 1.5^2
            Fix32.FromFraction(225, 100).Raw - 2, Fix32.FromFraction(225, 100).Raw + 2);
    }

    [Fact]
    public void AGrowthFromBelowOne_FailsToLoad()
        => Assert.Throws<ContentException>(
            () => Load(SixWaves("\"appetiteGrowth\": 1.5, \"appetiteGrowthFrom\": 0")));

    [Fact]
    public void AnExplicitScale_StillOverridesGrowthFrom()
    {
        const string json = """
        { "map": "t", "appetiteGrowth": 1.5, "appetiteGrowthFrom": 4, "waves": [
            { "index": 1, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] },
            { "index": 2, "appetiteScale": 9.0, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 10 } ] } ] }
        """;
        Assert.Equal(Fix32.FromInt(9), Load(json)[1].AppetiteScale);
    }

    [Fact]
    public void HugeScale_DoesNotOverflowVisitorHealth()
    {
        // Fix32 tops out near 32767, so scaling via Fix32 multiply would wrap a
        // tough visitor late in a long table. SpawnSystem uses long math instead;
        // this is the guard on that decision.
        VisitorDef[] visitors = ContentLoader.LoadVisitors(new[]
            { ("hulk.json", """{ "id": "hulk", "name": "Hulk", "appetite": 20000, "speed": 0.04, "bounty": 50 }""") });

        WaveDef[] waves = ContentLoader.LoadWaves("""
        { "map": "t", "waves": [
            { "index": 1, "appetiteScale": 100.0, "entries": [ { "visitor": "hulk", "count": 1, "spacingTicks": 10 } ] } ] }
        """, visitors, "waves.json");

        long scaled = ((long)visitors[0].Appetite * waves[0].AppetiteScale.Raw) >> Fix32.FractionalBits;
        Assert.Equal(2_000_000, scaled);   // 20000 x 100, not a wrapped negative
    }
}
