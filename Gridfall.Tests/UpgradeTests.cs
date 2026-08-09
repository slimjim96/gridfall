using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Station upgrades: the gold sink. Before this existed, late gold ran to 1,090+
/// with nothing to buy once the board saturated.
/// </summary>
public class UpgradeTests
{
    private static (Sim sim, int stationId) SimWithStation()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.StationIndexOf("arrow-station")));
        sim.Tick();
        return (sim, sim.State.StationId(0));
    }

    [Fact]
    public void ANewStation_StartsAtLevelOne()
    {
        (Sim sim, _) = SimWithStation();
        Assert.Equal(1, sim.State.StationLevel(0));
    }

    [Fact]
    public void Upgrading_CostsGoldAndRaisesTheLevel()
    {
        (Sim sim, int stationId) = SimWithStation();
        int goldBefore = sim.State.Gold;
        int cost = sim.Content.Station(sim.State.StationDefIndex(0)).Upgrades[0].Cost;

        sim.Enqueue(new UpgradeCommand(stationId));
        sim.Tick();

        Assert.Equal(2, sim.State.StationLevel(0));
        Assert.Equal(goldBefore - cost, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e => e.Kind == EventKind.StationUpgraded && e.B == 2);
    }

    [Fact]
    public void Upgrading_RaisesServingAndRange()
    {
        StationDef def = TestContent.BuildContent().Stations.Single(t => t.Id == "arrow-station");

        Assert.True(def.ServingAt(2) > def.ServingAt(1));
        Assert.True(def.ServingAt(3) > def.ServingAt(2));
        Assert.Equal(def.RangeSquaredAt(1), def.RangeSquaredAt(2));      // level 2 is serving only
        Assert.True(def.RangeSquaredAt(3) > def.RangeSquaredAt(2));      // level 3 widens
    }

    [Fact]
    public void AtMaxLevel_FurtherUpgradesAreRefused()
    {
        (Sim sim, int stationId) = SimWithStation();
        sim.MutableState.Gold = 100000;

        StationDef def = sim.Content.Station(sim.State.StationDefIndex(0));
        for (int i = 0; i < def.MaxLevel - 1; i++) { sim.Enqueue(new UpgradeCommand(stationId)); sim.Tick(); }
        Assert.Equal(def.MaxLevel, sim.State.StationLevel(0));

        int goldBefore = sim.State.Gold;
        sim.Enqueue(new UpgradeCommand(stationId));
        sim.Tick();

        Assert.Equal(def.MaxLevel, sim.State.StationLevel(0));
        Assert.Equal(goldBefore, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.UpgradeRejected && e.A == (int)RejectReason.AlreadyMaxLevel);
    }

    [Fact]
    public void WithoutEnoughGold_TheUpgradeIsRefusedAndNothingIsSpent()
    {
        (Sim sim, int stationId) = SimWithStation();
        sim.MutableState.Gold = 5;

        sim.Enqueue(new UpgradeCommand(stationId));
        sim.Tick();

        Assert.Equal(1, sim.State.StationLevel(0));
        Assert.Equal(5, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.UpgradeRejected && e.A == (int)RejectReason.InsufficientGold);
    }

    [Fact]
    public void UpgradingThenSelling_IsNeverProfitable()
    {
        // The exploit guard. A flat base refund would make this a money printer.
        (Sim sim, int stationId) = SimWithStation();
        sim.MutableState.Gold = 100000;

        StationDef def = sim.Content.Station(sim.State.StationDefIndex(0));
        int goldBeforeUpgrades = sim.State.Gold;

        for (int i = 0; i < def.MaxLevel - 1; i++) { sim.Enqueue(new UpgradeCommand(stationId)); sim.Tick(); }
        int spent = goldBeforeUpgrades - sim.State.Gold;

        int goldBeforeSell = sim.State.Gold;
        sim.Enqueue(new SellCommand(stationId));
        sim.Tick();

        int refund = sim.State.Gold - goldBeforeSell;
        Assert.True(refund <= spent + def.Cost, $"refund {refund} exceeds total spend {spent + def.Cost}");
        Assert.True(refund > def.SellValueAt(1), "an upgraded station should refund more than a base one");
    }

    [Fact]
    public void UpgradingNeverChangesTheRouteOrDirtiesTheGrid()
    {
        // Criterion 11: an upgrade occupies the same cell, so it cannot seal a lane.
        (Sim sim, int stationId) = SimWithStation();
        sim.MutableState.Gold = 100000;

        ushort versionBefore = sim.Path.Version;
        byte[] costBefore = Enumerable.Range(0, sim.Map.Width * sim.Map.Height)
            .Select(i => (byte)(sim.Path.IsBlocked(i) ? 1 : 0)).ToArray();

        sim.Enqueue(new UpgradeCommand(stationId));
        sim.Tick();

        Assert.Equal(versionBefore, sim.Path.Version);
        byte[] costAfter = Enumerable.Range(0, sim.Map.Width * sim.Map.Height)
            .Select(i => (byte)(sim.Path.IsBlocked(i) ? 1 : 0)).ToArray();
        Assert.Equal(costBefore, costAfter);
    }

    [Fact]
    public void ServingPerGold_FallsWithEachLevel()
    {
        // Criterion 12, and the whole of pillar 5 here: if upgrading were more
        // efficient than building, nobody would spread out and mazing would stop
        // mattering. Asserted against the SHIPPED data, not the intent.
        foreach (StationDef def in TestContent.BuildContent().Stations)
        {
            if (def.Upgrades.Length == 0) continue;

            double baseline = def.Serving / (double)def.Cost;
            int spent = def.Cost;

            for (int level = 2; level <= def.MaxLevel; level++)
            {
                spent += def.Upgrades[level - 2].Cost;
                double atLevel = def.ServingAt(level) / (double)spent;
                Assert.True(atLevel < baseline,
                    $"{def.Id} level {level}: {atLevel:F3} dmg/gold is not below the base {baseline:F3} -- " +
                    "upgrading would dominate building and mazing would stop mattering");
            }
        }
    }

    [Fact]
    public void Hash_Covers_StationLevel()
    {
        (Sim sim, _) = SimWithStation();
        ulong before = sim.Hash();
        sim.MutableState.StationLevel[0] = 2;
        Assert.NotEqual(before, sim.Hash());
    }

    [Fact]
    public void Snapshot_PreservesStationLevels()
    {
        (Sim sim, int stationId) = SimWithStation();
        sim.MutableState.Gold = 100000;
        sim.Enqueue(new UpgradeCommand(stationId));
        sim.Tick();

        SimSnapshot snap = sim.Snapshot();
        sim.Enqueue(new UpgradeCommand(stationId));
        sim.Tick();
        Assert.Equal(3, sim.State.StationLevel(0));

        sim.Restore(snap);
        Assert.Equal(2, sim.State.StationLevel(0));
    }

    [Fact]
    public void UpgradingASoldStation_IsRefusedNotCrashed()
    {
        (Sim sim, int stationId) = SimWithStation();
        sim.Enqueue(new SellCommand(stationId));
        sim.Tick();

        sim.Enqueue(new UpgradeCommand(stationId));
        sim.Tick();

        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.UpgradeRejected && e.A == (int)RejectReason.NoSuchStation);
    }
}
