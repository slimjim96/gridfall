using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Selling refunds half of what is LEFT of a tower, not half of what it cost.
///
/// Before this, cashing out a nearly-destroyed tower paid the same as cashing out
/// a pristine one, which made pre-empting every destruction strictly profitable
/// and drove towers-destroyed-per-run to zero.
///
/// The tests that matter are the two ends: a wreck must pay almost nothing, and
/// an undamaged tower must pay EXACTLY what it always did. The second is pillar 1
/// -- repositioning is the maze mechanic and this slice must be invisible to it.
/// </summary>
public class SalvageTests
{
    private static (Sim sim, int towerId, TowerDef def) SimWithTower()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Tick();
        return (sim, sim.State.TowerId(0), sim.Content.Tower(sim.State.TowerDefIndex(0)));
    }

    private static int RefundFromSelling(Sim sim, int towerId)
    {
        int before = sim.State.Gold;
        sim.Enqueue(new SellCommand(towerId));
        sim.Tick();
        return sim.State.Gold - before;
    }

    // ---- pillar 1: repositioning must not notice this slice ----------------

    [Fact]
    public void SellingAnUndamagedTower_RefundsExactlyWhatItAlwaysDid()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithTower();
        Assert.Equal(def.Hp, sim.State.TowerHp(0));

        Assert.Equal(def.SellValueAt(1), RefundFromSelling(sim, towerId));
    }

    [Fact]
    public void AtEveryLevel_AnUndamagedTowerRefundsTheUnscaledValue()
    {
        // Asserted directly rather than inferred from the balance run. Pillar 1
        // should not rest on `a * b / b == a` holding for every input.
        foreach (TowerDef def in TestContent.BuildContent().Towers)
            for (int level = 1; level <= def.MaxLevel; level++)
                Assert.Equal(def.SellValueAt(level), def.SalvageValueAt(level, def.Hp));
    }

    [Fact]
    public void AnUpgradedUndamagedTower_StillRefundsHalfOfEverythingSpent()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithTower();
        sim.MutableState.Gold = 100000;
        for (int i = 0; i < def.MaxLevel - 1; i++) { sim.Enqueue(new UpgradeCommand(towerId)); sim.Tick(); }
        Assert.Equal(def.MaxLevel, sim.State.TowerLevel(0));

        Assert.Equal(def.SellValueAt(def.MaxLevel), RefundFromSelling(sim, towerId));
    }

    // ---- the mechanic ------------------------------------------------------

    [Fact]
    public void SellingADamagedTower_RefundsStrictlyLess()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithTower();
        sim.MutableState.TowerHp[0] = def.Hp / 2;

        int refund = RefundFromSelling(sim, towerId);
        Assert.True(refund < def.SellValueAt(1), $"refund {refund} should be under {def.SellValueAt(1)}");
        Assert.Equal(def.SellValueAt(1) / 2, refund);
    }

    [Fact]
    public void RefundScalesWithHealthRemaining()
    {
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");

        int quarter = def.SalvageValueAt(1, def.Hp / 4);
        int half = def.SalvageValueAt(1, def.Hp / 2);
        int full = def.SalvageValueAt(1, def.Hp);

        Assert.True(quarter < half, $"{quarter} !< {half}");
        Assert.True(half < full, $"{half} !< {full}");
    }

    [Fact]
    public void AWreckRefundsAlmostNothing_AndNeverANegativeAmount()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithTower();
        sim.MutableState.TowerHp[0] = 1;

        int refund = RefundFromSelling(sim, towerId);
        Assert.InRange(refund, 0, def.SellValueAt(1) / 10);
    }

    [Fact]
    public void ATowerAtZeroHealth_RefundsNothing()
    {
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");
        Assert.Equal(0, def.SalvageValueAt(1, 0));
        Assert.Equal(0, def.SalvageValueAt(1, -50));   // guard, not arithmetic
    }

    [Fact]
    public void RefundNeverExceedsTotalSpend_AtAnyHealthOrLevel()
    {
        foreach (TowerDef def in TestContent.BuildContent().Towers)
            for (int level = 1; level <= def.MaxLevel; level++)
                for (int hp = 0; hp <= def.Hp; hp += System.Math.Max(1, def.Hp / 20))
                    Assert.True(def.SalvageValueAt(level, hp) <= def.TotalSpentAt(level),
                        $"{def.Id} L{level} at {hp} hp refunded more than it cost");
    }

    [Fact]
    public void AnUpgradedDamagedTower_ScalesTheUpgradeCostsToo()
    {
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");

        // The level-3 refund must still be worth more than the level-1 one at the
        // same damage, or upgrading would be safer than not upgrading.
        Assert.True(def.SalvageValueAt(3, def.Hp / 2) > def.SalvageValueAt(1, def.Hp / 2));
    }

    // ---- availability ------------------------------------------------------

    [Fact]
    public void SellingStillWorksWhileAWaveIsRunning()
    {
        // Deliberately unlike repair. Pricing the retreat, not forbidding it --
        // forbidding it measured worse on standing defence and coverage alike.
        (Sim sim, int towerId, TowerDef def) = SimWithTower();
        sim.MutableState.TowerHp[0] = def.Hp / 2;
        sim.Enqueue(new StartWaveCommand());
        sim.Tick();
        Assert.True(sim.State.WaveActive);

        Assert.True(RefundFromSelling(sim, towerId) > 0);
        Assert.Equal(0, sim.State.TowerCount);
    }

    [Fact]
    public void ADestroyedTowerRefundsNothing_BecauseThereIsNothingToSell()
    {
        (Sim sim, int towerId, _) = SimWithTower();
        sim.Enqueue(new SellCommand(towerId));
        sim.Tick();

        Assert.Equal(0, RefundFromSelling(sim, towerId));   // selling twice is silent
    }

    // ---- arithmetic --------------------------------------------------------

    [Fact]
    public void SalvageValue_DoesNotOverflowAtExtremeValues()
    {
        TowerDef[] towers = ContentLoader.LoadTowers(new[]
        {
            ("huge.json", """
            { "id": "huge", "name": "Huge", "cost": 1000000, "range": 1.0, "cooldown": 1.0,
              "damage": 1, "projectileSpeed": 1.0, "targeting": "nearest",
              "sellValue": 500000, "hp": 1000000, "repairPercent": 99 }
            """),
        });

        Assert.Equal(250_000, towers[0].SalvageValueAt(1, 500_000));
    }

    [Fact]
    public void SalvageRoundsDown_TheOppositeOfRepairAndForTheSameReason()
    {
        // Repair rounds UP because the player pays it; salvage rounds DOWN because
        // the player receives it. Both round against the player, which is what
        // closes granularity exploits at either end.
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");

        // arrow: spent 50, sell 25, hp 100. At 3 hp: 25 * 3 / 100 = 0.75 -> 0.
        Assert.Equal(0, def.SalvageValueAt(1, 3));
    }

    // ---- determinism -------------------------------------------------------

    [Fact]
    public void SellingADamagedTower_IsDeterministicAcrossRuns()
    {
        Assert.Equal(RunOne(), RunOne());

        static ulong RunOne()
        {
            (Sim sim, int towerId, TowerDef def) = SimWithTower();
            sim.MutableState.TowerHp[0] = def.Hp / 3;
            sim.Enqueue(new SellCommand(towerId));
            for (int t = 0; t < 50; t++) sim.Tick();
            return sim.Hash();
        }
    }
}
