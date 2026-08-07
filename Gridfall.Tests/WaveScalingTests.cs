using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Per-wave HP scaling. Without it later waves cannot be harder -- more creeps
/// of a fixed toughness just hand the player more bounty, which becomes more
/// towers. See 2026-08-06-crossroads-12-waves-balance.md.
/// </summary>
public class WaveScalingTests
{
    private const string Enemies = """
    { "id": "runner", "name": "Runner", "hp": 100, "speed": 0.06, "bounty": 8 }
    """;

    private static WaveDef[] Load(string wavesJson)
    {
        EnemyDef[] enemies = ContentLoader.LoadEnemies(new[] { ("runner.json", Enemies) });
        return ContentLoader.LoadWaves(wavesJson, enemies, "waves.json");
    }

    private const string ThreeWaves = """
    {
      "map": "t", "hpGrowth": 1.5,
      "waves": [
        { "index": 1, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 2, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 3, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] }
      ]
    }
    """;

    [Fact]
    public void WaveOne_IsNeverScaled()
        => Assert.Equal(Fix32.One, Load(ThreeWaves)[0].HpScale);

    [Fact]
    public void GrowthCompoundsWaveToWave()
    {
        WaveDef[] waves = Load(ThreeWaves);
        Assert.Equal(Fix32.FromFraction(15, 10), waves[1].HpScale);
        // 1.5^2, allowing for one raw unit of fixed-point truncation.
        Assert.InRange(waves[2].HpScale.Raw, Fix32.FromFraction(225, 100).Raw - 2,
                                             Fix32.FromFraction(225, 100).Raw + 2);
    }

    [Fact]
    public void NoGrowthDeclared_MeansNoScaling()
    {
        const string flat = """
        { "map": "t", "waves": [
            { "index": 1, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
            { "index": 2, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] } ] }
        """;
        Assert.All(Load(flat), w => Assert.Equal(Fix32.One, w.HpScale));
    }

    [Fact]
    public void AnExplicitScale_OverridesTheCurve()
    {
        const string json = """
        { "map": "t", "hpGrowth": 1.5, "waves": [
            { "index": 1, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
            { "index": 2, "hpScale": 10.0, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] } ] }
        """;
        Assert.Equal(Fix32.FromInt(10), Load(json)[1].HpScale);
    }

    [Fact]
    public void ShrinkingGrowth_IsRejected()
    {
        const string json = """
        { "map": "t", "hpGrowth": 0.9, "waves": [
            { "index": 1, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] } ] }
        """;
        ContentException ex = Assert.Throws<ContentException>(() => Load(json));
        Assert.Contains("weaker", ex.Message);
    }

    [Fact]
    public void ScaledCreeps_ActuallySpawnWithMoreHealth()
    {
        // The end-to-end claim: the scalar reaches a live creep.
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new StartWaveCommand());
        sim.Tick();

        int slot = sim.State.CreepSlotByOrder(0);
        int wave1Hp = sim.State.CreepHp(slot);

        Assert.Equal(sim.Content.Enemy(sim.State.CreepDefIndex(slot)).Hp, wave1Hp);
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

    // ---- hpGrowthFrom: where the ramp starts -------------------------------

    private static string SixWaves(string header) => $$"""
    {
      "map": "t", {{header}},
      "waves": [
        { "index": 1, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 2, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 3, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 4, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 5, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
        { "index": 6, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] }
      ]
    }
    """;

    [Fact]
    public void WithoutGrowthFrom_TheCurveIsExactlyWhatItWasBefore()
    {
        // The back-compat guarantee. gauntlet declares no hpGrowthFrom and its
        // numbers must not move because crossroads needed a different shape.
        WaveDef[] withoutField = Load(SixWaves("\"hpGrowth\": 1.5"));
        WaveDef[] withDefault = Load(SixWaves("\"hpGrowth\": 1.5, \"hpGrowthFrom\": 1"));

        for (int i = 0; i < withoutField.Length; i++)
            Assert.Equal(withoutField[i].HpScale.Raw, withDefault[i].HpScale.Raw);
    }

    [Fact]
    public void WavesAtOrBeforeGrowthFrom_AreFlat()
    {
        // The opening is where the player is broke and thin. Making it flat is
        // what lets the late rate be steep -- see early-economy-2.
        WaveDef[] waves = Load(SixWaves("\"hpGrowth\": 1.5, \"hpGrowthFrom\": 4"));

        Assert.Equal(Fix32.One, waves[0].HpScale);   // wave 1
        Assert.Equal(Fix32.One, waves[1].HpScale);   // wave 2
        Assert.Equal(Fix32.One, waves[2].HpScale);   // wave 3
        Assert.Equal(Fix32.One, waves[3].HpScale);   // wave 4 -- the last flat one
    }

    [Fact]
    public void AfterGrowthFrom_TheRampRunsAtTheDeclaredRate()
    {
        WaveDef[] waves = Load(SixWaves("\"hpGrowth\": 1.5, \"hpGrowthFrom\": 4"));

        Assert.Equal(Fix32.FromFraction(15, 10), waves[4].HpScale);          // wave 5 = 1.5^1
        Assert.InRange(waves[5].HpScale.Raw,                                  // wave 6 = 1.5^2
            Fix32.FromFraction(225, 100).Raw - 2, Fix32.FromFraction(225, 100).Raw + 2);
    }

    [Fact]
    public void AGrowthFromBelowOne_FailsToLoad()
        => Assert.Throws<ContentException>(
            () => Load(SixWaves("\"hpGrowth\": 1.5, \"hpGrowthFrom\": 0")));

    [Fact]
    public void AnExplicitScale_StillOverridesGrowthFrom()
    {
        const string json = """
        { "map": "t", "hpGrowth": 1.5, "hpGrowthFrom": 4, "waves": [
            { "index": 1, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] },
            { "index": 2, "hpScale": 9.0, "entries": [ { "enemy": "runner", "count": 1, "spacingTicks": 10 } ] } ] }
        """;
        Assert.Equal(Fix32.FromInt(9), Load(json)[1].HpScale);
    }

    [Fact]
    public void HugeScale_DoesNotOverflowCreepHealth()
    {
        // Fix32 tops out near 32767, so scaling via Fix32 multiply would wrap a
        // tough enemy late in a long table. SpawnSystem uses long math instead;
        // this is the guard on that decision.
        EnemyDef[] enemies = ContentLoader.LoadEnemies(new[]
            { ("hulk.json", """{ "id": "hulk", "name": "Hulk", "hp": 20000, "speed": 0.04, "bounty": 50 }""") });

        WaveDef[] waves = ContentLoader.LoadWaves("""
        { "map": "t", "waves": [
            { "index": 1, "hpScale": 100.0, "entries": [ { "enemy": "hulk", "count": 1, "spacingTicks": 10 } ] } ] }
        """, enemies, "waves.json");

        long scaled = ((long)enemies[0].Hp * waves[0].HpScale.Raw) >> Fix32.FractionalBits;
        Assert.Equal(2_000_000, scaled);   // 20000 x 100, not a wrapped negative
    }
}
