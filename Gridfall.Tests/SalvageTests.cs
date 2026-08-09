using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Selling refunds half of what is LEFT of a station, not half of what it cost.
///
/// Before this, cashing out a nearly-destroyed station paid the same as cashing out
/// a pristine one, which made pre-empting every destruction strictly profitable
/// and drove stations-destroyed-per-run to zero.
///
/// The tests that matter are the two ends: a wreck must pay almost nothing, and
/// an undepleted station must pay EXACTLY what it always did. The second is pillar 1
/// -- repositioning is the maze mechanic and this slice must be invisible to it.
/// </summary>
public class SalvageTests
{
    private static (Sim sim, int stationId, StationDef def) SimWithStation()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.StationIndexOf("arrow-station")));
        sim.Tick();
        return (sim, sim.State.StationId(0), sim.Content.Station(sim.State.StationDefIndex(0)));
    }

    private static int RefundFromSelling(Sim sim, int stationId)
    {
        int before = sim.State.Gold;
        sim.Enqueue(new SellCommand(stationId));
        sim.Tick();
        return sim.State.Gold - before;
    }

    // ---- pillar 1: repositioning must not notice this slice ----------------

    [Fact]
    public void SellingAnUndepletedStation_RefundsExactlyWhatItAlwaysDid()
    {
        (Sim sim, int stationId, StationDef def) = SimWithStation();
        Assert.Equal(def.Stock, sim.State.StationStock(0));

        Assert.Equal(def.SellValueAt(1), RefundFromSelling(sim, stationId));
    }

    [Fact]
    public void AtEveryLevel_AnUndepletedStationRefundsTheUnscaledValue()
    {
        // Asserted directly rather than inferred from the balance run. Pillar 1
        // should not rest on `a * b / b == a` holding for every input.
        foreach (StationDef def in TestContent.BuildContent().Stations)
            for (int level = 1; level <= def.MaxLevel; level++)
                Assert.Equal(def.SellValueAt(level), def.SalvageValueAt(level, def.Stock));
    }

    [Fact]
    public void AnUpgradedUndepletedStation_StillRefundsHalfOfEverythingSpent()
    {
        (Sim sim, int stationId, StationDef def) = SimWithStation();
        sim.MutableState.Gold = 100000;
        for (int i = 0; i < def.MaxLevel - 1; i++) { sim.Enqueue(new UpgradeCommand(stationId)); sim.Tick(); }
        Assert.Equal(def.MaxLevel, sim.State.StationLevel(0));

        Assert.Equal(def.SellValueAt(def.MaxLevel), RefundFromSelling(sim, stationId));
    }

    // ---- the mechanic ------------------------------------------------------

    [Fact]
    public void SellingADepletedStation_RefundsStrictlyLess()
    {
        (Sim sim, int stationId, StationDef def) = SimWithStation();
        sim.MutableState.StationStock[0] = def.Stock / 2;

        int refund = RefundFromSelling(sim, stationId);
        Assert.True(refund < def.SellValueAt(1), $"refund {refund} should be under {def.SellValueAt(1)}");
        Assert.Equal(def.SellValueAt(1) / 2, refund);
    }

    [Fact]
    public void RefundScalesWithHealthRemaining()
    {
        StationDef def = TestContent.BuildContent().Stations.Single(t => t.Id == "arrow-station");

        int quarter = def.SalvageValueAt(1, def.Stock / 4);
        int half = def.SalvageValueAt(1, def.Stock / 2);
        int full = def.SalvageValueAt(1, def.Stock);

        Assert.True(quarter < half, $"{quarter} !< {half}");
        Assert.True(half < full, $"{half} !< {full}");
    }

    [Fact]
    public void AWreckRefundsAlmostNothing_AndNeverANegativeAmount()
    {
        (Sim sim, int stationId, StationDef def) = SimWithStation();
        sim.MutableState.StationStock[0] = 1;

        int refund = RefundFromSelling(sim, stationId);
        Assert.InRange(refund, 0, def.SellValueAt(1) / 10);
    }

    [Fact]
    public void AStationAtZeroHealth_RefundsNothing()
    {
        StationDef def = TestContent.BuildContent().Stations.Single(t => t.Id == "arrow-station");
        Assert.Equal(0, def.SalvageValueAt(1, 0));
        Assert.Equal(0, def.SalvageValueAt(1, -50));   // guard, not arithmetic
    }

    [Fact]
    public void RefundNeverExceedsTotalSpend_AtAnyHealthOrLevel()
    {
        foreach (StationDef def in TestContent.BuildContent().Stations)
            for (int level = 1; level <= def.MaxLevel; level++)
                for (int hp = 0; hp <= def.Stock; hp += System.Math.Max(1, def.Stock / 20))
                    Assert.True(def.SalvageValueAt(level, hp) <= def.TotalSpentAt(level),
                        $"{def.Id} L{level} at {hp} hp refunded more than it cost");
    }

    [Fact]
    public void AnUpgradedDepletedStation_ScalesTheUpgradeCostsToo()
    {
        StationDef def = TestContent.BuildContent().Stations.Single(t => t.Id == "arrow-station");

        // The level-3 refund must still be worth more than the level-1 one at the
        // same serving, or upgrading would be safer than not upgrading.
        Assert.True(def.SalvageValueAt(3, def.Stock / 2) > def.SalvageValueAt(1, def.Stock / 2));
    }

    // ---- availability ------------------------------------------------------

    [Fact]
    public void SellingStillWorksWhileAWaveIsRunning()
    {
        // Deliberately unlike repair. Pricing the retreat, not forbidding it --
        // forbidding it measured worse on standing defence and coverage alike.
        (Sim sim, int stationId, StationDef def) = SimWithStation();
        sim.MutableState.StationStock[0] = def.Stock / 2;
        sim.Enqueue(new StartWaveCommand());
        sim.Tick();
        Assert.True(sim.State.WaveActive);

        Assert.True(RefundFromSelling(sim, stationId) > 0);
        Assert.Equal(0, sim.State.StationCount);
    }

    [Fact]
    public void ADestroyedStationRefundsNothing_BecauseThereIsNothingToSell()
    {
        (Sim sim, int stationId, _) = SimWithStation();
        sim.Enqueue(new SellCommand(stationId));
        sim.Tick();

        Assert.Equal(0, RefundFromSelling(sim, stationId));   // selling twice is silent
    }

    // ---- arithmetic --------------------------------------------------------

    [Fact]
    public void SalvageValue_DoesNotOverflowAtExtremeValues()
    {
        StationDef[] stations = ContentLoader.LoadStations(new[]
        {
            ("huge.json", """
            { "id": "huge", "name": "Huge", "cost": 1000000, "range": 1.0, "cooldown": 1.0,
              "serving": 1, "projectileSpeed": 1.0, "targeting": "nearest",
              "sellValue": 500000, "stock": 1000000, "repairPercent": 99 }
            """),
        });

        Assert.Equal(250_000, stations[0].SalvageValueAt(1, 500_000));
    }

    [Fact]
    public void SalvageRoundsDown_TheOppositeOfRepairAndForTheSameReason()
    {
        // Repair rounds UP because the player pays it; salvage rounds DOWN because
        // the player receives it. Both round against the player, which is what
        // closes granularity exploits at either end.
        StationDef def = TestContent.BuildContent().Stations.Single(t => t.Id == "arrow-station");

        // arrow: spent 50, sell 25, hp 100. At 3 hp: 25 * 3 / 100 = 0.75 -> 0.
        Assert.Equal(0, def.SalvageValueAt(1, 3));
    }

    // ---- determinism -------------------------------------------------------

    [Fact]
    public void SellingADepletedStation_IsDeterministicAcrossRuns()
    {
        Assert.Equal(RunOne(), RunOne());

        static ulong RunOne()
        {
            (Sim sim, int stationId, StationDef def) = SimWithStation();
            sim.MutableState.StationStock[0] = def.Stock / 3;
            sim.Enqueue(new SellCommand(stationId));
            for (int t = 0; t < 50; t++) sim.Tick();
            return sim.Hash();
        }
    }
}
