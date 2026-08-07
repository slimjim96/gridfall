using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Tower upgrades: the gold sink. Before this existed, late gold ran to 1,090+
/// with nothing to buy once the board saturated.
/// </summary>
public class UpgradeTests
{
    private static (Sim sim, int towerId) SimWithTower()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Tick();
        return (sim, sim.State.TowerId(0));
    }

    [Fact]
    public void ANewTower_StartsAtLevelOne()
    {
        (Sim sim, _) = SimWithTower();
        Assert.Equal(1, sim.State.TowerLevel(0));
    }

    [Fact]
    public void Upgrading_CostsGoldAndRaisesTheLevel()
    {
        (Sim sim, int towerId) = SimWithTower();
        int goldBefore = sim.State.Gold;
        int cost = sim.Content.Tower(sim.State.TowerDefIndex(0)).Upgrades[0].Cost;

        sim.Enqueue(new UpgradeCommand(towerId));
        sim.Tick();

        Assert.Equal(2, sim.State.TowerLevel(0));
        Assert.Equal(goldBefore - cost, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e => e.Kind == EventKind.TowerUpgraded && e.B == 2);
    }

    [Fact]
    public void Upgrading_RaisesDamageAndRange()
    {
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");

        Assert.True(def.DamageAt(2) > def.DamageAt(1));
        Assert.True(def.DamageAt(3) > def.DamageAt(2));
        Assert.Equal(def.RangeSquaredAt(1), def.RangeSquaredAt(2));      // level 2 is damage only
        Assert.True(def.RangeSquaredAt(3) > def.RangeSquaredAt(2));      // level 3 widens
    }

    [Fact]
    public void AtMaxLevel_FurtherUpgradesAreRefused()
    {
        (Sim sim, int towerId) = SimWithTower();
        sim.MutableState.Gold = 100000;

        TowerDef def = sim.Content.Tower(sim.State.TowerDefIndex(0));
        for (int i = 0; i < def.MaxLevel - 1; i++) { sim.Enqueue(new UpgradeCommand(towerId)); sim.Tick(); }
        Assert.Equal(def.MaxLevel, sim.State.TowerLevel(0));

        int goldBefore = sim.State.Gold;
        sim.Enqueue(new UpgradeCommand(towerId));
        sim.Tick();

        Assert.Equal(def.MaxLevel, sim.State.TowerLevel(0));
        Assert.Equal(goldBefore, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.UpgradeRejected && e.A == (int)RejectReason.AlreadyMaxLevel);
    }

    [Fact]
    public void WithoutEnoughGold_TheUpgradeIsRefusedAndNothingIsSpent()
    {
        (Sim sim, int towerId) = SimWithTower();
        sim.MutableState.Gold = 5;

        sim.Enqueue(new UpgradeCommand(towerId));
        sim.Tick();

        Assert.Equal(1, sim.State.TowerLevel(0));
        Assert.Equal(5, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.UpgradeRejected && e.A == (int)RejectReason.InsufficientGold);
    }

    [Fact]
    public void UpgradingThenSelling_IsNeverProfitable()
    {
        // The exploit guard. A flat base refund would make this a money printer.
        (Sim sim, int towerId) = SimWithTower();
        sim.MutableState.Gold = 100000;

        TowerDef def = sim.Content.Tower(sim.State.TowerDefIndex(0));
        int goldBeforeUpgrades = sim.State.Gold;

        for (int i = 0; i < def.MaxLevel - 1; i++) { sim.Enqueue(new UpgradeCommand(towerId)); sim.Tick(); }
        int spent = goldBeforeUpgrades - sim.State.Gold;

        int goldBeforeSell = sim.State.Gold;
        sim.Enqueue(new SellCommand(towerId));
        sim.Tick();

        int refund = sim.State.Gold - goldBeforeSell;
        Assert.True(refund <= spent + def.Cost, $"refund {refund} exceeds total spend {spent + def.Cost}");
        Assert.True(refund > def.SellValueAt(1), "an upgraded tower should refund more than a base one");
    }

    [Fact]
    public void UpgradingNeverChangesTheRouteOrDirtiesTheGrid()
    {
        // Criterion 11: an upgrade occupies the same cell, so it cannot seal a lane.
        (Sim sim, int towerId) = SimWithTower();
        sim.MutableState.Gold = 100000;

        ushort versionBefore = sim.Path.Version;
        byte[] costBefore = Enumerable.Range(0, sim.Map.Width * sim.Map.Height)
            .Select(i => (byte)(sim.Path.IsBlocked(i) ? 1 : 0)).ToArray();

        sim.Enqueue(new UpgradeCommand(towerId));
        sim.Tick();

        Assert.Equal(versionBefore, sim.Path.Version);
        byte[] costAfter = Enumerable.Range(0, sim.Map.Width * sim.Map.Height)
            .Select(i => (byte)(sim.Path.IsBlocked(i) ? 1 : 0)).ToArray();
        Assert.Equal(costBefore, costAfter);
    }

    [Fact]
    public void DamagePerGold_FallsWithEachLevel()
    {
        // Criterion 12, and the whole of pillar 5 here: if upgrading were more
        // efficient than building, nobody would spread out and mazing would stop
        // mattering. Asserted against the SHIPPED data, not the intent.
        foreach (TowerDef def in TestContent.BuildContent().Towers)
        {
            if (def.Upgrades.Length == 0) continue;

            double baseline = def.Damage / (double)def.Cost;
            int spent = def.Cost;

            for (int level = 2; level <= def.MaxLevel; level++)
            {
                spent += def.Upgrades[level - 2].Cost;
                double atLevel = def.DamageAt(level) / (double)spent;
                Assert.True(atLevel < baseline,
                    $"{def.Id} level {level}: {atLevel:F3} dmg/gold is not below the base {baseline:F3} -- " +
                    "upgrading would dominate building and mazing would stop mattering");
            }
        }
    }

    [Fact]
    public void Hash_Covers_TowerLevel()
    {
        (Sim sim, _) = SimWithTower();
        ulong before = sim.Hash();
        sim.MutableState.TowerLevel[0] = 2;
        Assert.NotEqual(before, sim.Hash());
    }

    [Fact]
    public void Snapshot_PreservesTowerLevels()
    {
        (Sim sim, int towerId) = SimWithTower();
        sim.MutableState.Gold = 100000;
        sim.Enqueue(new UpgradeCommand(towerId));
        sim.Tick();

        SimSnapshot snap = sim.Snapshot();
        sim.Enqueue(new UpgradeCommand(towerId));
        sim.Tick();
        Assert.Equal(3, sim.State.TowerLevel(0));

        sim.Restore(snap);
        Assert.Equal(2, sim.State.TowerLevel(0));
    }

    [Fact]
    public void UpgradingASoldTower_IsRefusedNotCrashed()
    {
        (Sim sim, int towerId) = SimWithTower();
        sim.Enqueue(new SellCommand(towerId));
        sim.Tick();

        sim.Enqueue(new UpgradeCommand(towerId));
        sim.Tick();

        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.UpgradeRejected && e.A == (int)RejectReason.NoSuchTower);
    }
}
