using Gridfall.Core;
using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Wave variance shifts when each group of a wave starts, and nothing else.
///
/// The point of the design is what it does NOT touch: composition, counts and
/// spacing are untouched, so the authored difficulty curve survives and a varied
/// wave stays explicable (pillar 4).
/// </summary>
public class WaveVarianceTests
{
    private static ContentSet WithVariance(int percent)
    {
        ContentSet baseline = TestContent.BuildContent();
        WaveDef[] waves = baseline.Waves
            .Select(w => new WaveDef
            {
                Index = w.Index, Entries = w.Entries, AppetiteScale = w.AppetiteScale, VariancePercent = percent,
            })
            .ToArray();

        return new ContentSet { Stations = baseline.Stations, Visitors = baseline.Visitors, Waves = waves };
    }

    private static int[] StartTicks(ContentSet content, uint seed)
    {
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), content, seed);
        sim.Enqueue(new StartWaveCommand());
        sim.Tick();
        var ticks = new int[SimState.MaxWaveEntries];
        for (int i = 0; i < ticks.Length; i++) ticks[i] = sim.State.WaveEntryNextTick(i);
        return ticks;
    }

    [Fact]
    public void ZeroVariance_DrawsNoRandomNumbers()
    {
        // Load-bearing, not an optimisation. SimRandom's state is hashed, so a
        // draw taken while the feature is off would change every recorded trace
        // for no behaviour at all.
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), WithVariance(0), 1);
        uint before = sim.Random.Draws;

        sim.Enqueue(new StartWaveCommand());
        sim.Tick();

        Assert.Equal(before, sim.Random.Draws);
    }

    [Fact]
    public void Variance_ActuallyChangesTheSchedule()
    {
        int[] fixedRun = StartTicks(WithVariance(0), seed: 7);
        int[] varied = StartTicks(WithVariance(100), seed: 7);

        Assert.NotEqual(fixedRun, varied);
    }

    [Fact]
    public void SameSeedIsIdentical_DifferentSeedsDiffer()
    {
        // Unpredictable to the player, exactly repeatable for the harness. That
        // is the whole reason variance is drawn from the run seed.
        Assert.Equal(StartTicks(WithVariance(100), 11), StartTicks(WithVariance(100), 11));
        Assert.NotEqual(StartTicks(WithVariance(100), 11), StartTicks(WithVariance(100), 12));
    }

    [Fact]
    public void Variance_NeverPullsAGroupEarlierThanAuthored()
    {
        // Jitter is a delay, never an advance. Pulling a group earlier could put
        // an visitor on the board before the wave was meant to have started.
        int[] authored = StartTicks(WithVariance(0), 3);
        int[] varied = StartTicks(WithVariance(100), 3);

        for (int i = 0; i < authored.Length; i++)
            Assert.True(varied[i] >= authored[i],
                $"entry {i} started at {varied[i]}, earlier than the authored {authored[i]}");
    }

    [Fact]
    public void WaveVariance_OutsideRangeIsRefused()
    {
        VisitorDef[] visitors = TestContent.BuildContent().Visitors.ToArray();
        const string body = """
        { "waves": [ { "index": 1, "entries": [ { "visitor": "runner", "count": 1, "spacingTicks": 5 } ] } ]
        """;

        foreach (int bad in new[] { -1, 101 })
            Assert.Throws<ContentException>(
                () => ContentLoader.LoadWaves(body + $", \"waveVariance\": {bad} }}", visitors, "t.json"));

        // The ends are legal.
        Assert.Equal(0, ContentLoader.LoadWaves(body + ", \"waveVariance\": 0 }", visitors, "t.json")[0].VariancePercent);
        Assert.Equal(100, ContentLoader.LoadWaves(body + ", \"waveVariance\": 100 }", visitors, "t.json")[0].VariancePercent);
    }
}
