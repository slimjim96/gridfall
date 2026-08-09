using Gridfall.Core;
using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The HUD quotes a price and the sim charges one. They must be the same number,
/// which is why both call <see cref="Gridfall.Core.Systems.CommandSystem.BuildCost"/>
/// rather than each computing the premium.
/// </summary>
public class BuildPricingTests
{
    private static ContentSet WithPremium(int percent)
    {
        ContentSet baseline = TestContent.BuildContent();
        return new ContentSet
        {
            Stations = baseline.Stations,
            Visitors = baseline.Visitors,
            Waves = baseline.Waves.Select(w => new WaveDef
            {
                Index = w.Index, Entries = w.Entries, AppetiteScale = w.AppetiteScale,
                MidWaveBuildPercent = percent,
            }).ToArray(),
        };
    }

    [Fact]
    public void BetweenWaves_ThePriceIsTheBasePrice()
    {
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), WithPremium(150), 1);
        ushort station = sim.Content.StationIndexOf("arrow-station");

        Assert.False(sim.State.WaveActive);
        Assert.Equal(sim.Content.Station(station).Cost, sim.BuildCostOf(station));
    }

    [Fact]
    public void DuringAWave_ThePremiumApplies()
    {
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), WithPremium(150), 1);
        ushort station = sim.Content.StationIndexOf("arrow-station");

        sim.Enqueue(new StartWaveCommand());
        sim.Tick();

        Assert.True(sim.State.WaveActive);
        Assert.Equal(sim.Content.Station(station).Cost * 150 / 100, sim.BuildCostOf(station));
    }

    [Fact]
    public void ThePriceQuotedIsThePriceCharged()
    {
        // The whole point. A HUD showing one number while the sim deducts
        // another is worse than showing nothing.
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), WithPremium(150), 1);
        ushort station = sim.Content.StationIndexOf("arrow-station");

        sim.Enqueue(new StartWaveCommand());
        sim.Tick();

        int quoted = sim.BuildCostOf(station);
        int before = sim.State.Gold;

        sim.Enqueue(new BuildCommand(new GridCell(2, 2), station));
        sim.Tick();

        Assert.Equal(before - quoted, sim.State.Gold);
    }

    [Fact]
    public void NoPremiumConfigured_MeansNoPriceChangeEver()
    {
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), WithPremium(100), 1);
        ushort station = sim.Content.StationIndexOf("arrow-station");
        int baseCost = sim.Content.Station(station).Cost;

        Assert.Equal(baseCost, sim.BuildCostOf(station));
        sim.Enqueue(new StartWaveCommand());
        sim.Tick();
        Assert.Equal(baseCost, sim.BuildCostOf(station));
    }
}
