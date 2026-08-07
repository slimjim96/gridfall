using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Tower repair: the answer to destruction that is not "rebuild it".
///
/// The tests that matter here are not "health goes up". They are the two walls
/// the cost curve sits between -- repair must beat sell-and-rebuild, and repair
/// must not switch destruction off -- plus the between-waves rule that turned
/// out to be the only thing making the second wall hold.
/// </summary>
public class RepairTests
{
    private static (Sim sim, int towerId, TowerDef def) SimWithDamagedTower(int hpNow)
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Tick();

        int towerId = sim.State.TowerId(0);
        sim.MutableState.TowerHp[0] = hpNow;
        return (sim, towerId, sim.Content.Tower(sim.State.TowerDefIndex(0)));
    }

    // ---- the mechanic -----------------------------------------------------

    [Fact]
    public void ADamagedTower_IsRestoredToFullForGold()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithDamagedTower(def_hp_half());
        int goldBefore = sim.State.Gold;
        int expected = def.RepairCostFor(1, def.Hp - def_hp_half());

        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        Assert.Equal(def.Hp, sim.State.TowerHp(0));
        Assert.Equal(goldBefore - expected, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e => e.Kind == EventKind.TowerRepaired);

        int def_hp_half() => 50;   // ArrowTower fixture has no "hp", so Hp defaults to 100
    }

    [Fact]
    public void RepairingNeverOvershootsMaximumHealth()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithDamagedTower(1);

        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        Assert.Equal(def.Hp, sim.State.TowerHp(0));
    }

    [Fact]
    public void AnUndamagedTower_IsRefusedAndNothingIsSpent()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithDamagedTower(hpNow: 100);
        Assert.Equal(def.Hp, sim.State.TowerHp(0));   // the fixture really is at full
        int goldBefore = sim.State.Gold;

        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        Assert.Equal(goldBefore, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.RepairRejected && e.A == (int)RejectReason.NotDamaged);
    }

    [Fact]
    public void WithoutEnoughGold_TheRepairIsRefusedAndNothingIsSpent()
    {
        (Sim sim, int towerId, _) = SimWithDamagedTower(1);
        sim.MutableState.Gold = 0;

        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        Assert.Equal(0, sim.State.Gold);
        Assert.Equal(1, sim.State.TowerHp(0));
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.RepairRejected && e.A == (int)RejectReason.InsufficientGold);
    }

    [Fact]
    public void ADestroyedTower_CannotBeRepaired()
    {
        (Sim sim, int towerId, _) = SimWithDamagedTower(1);
        sim.Enqueue(new SellCommand(towerId));
        sim.Tick();

        int goldBefore = sim.State.Gold;
        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        // Silent, like selling twice. There is no corpse to repair.
        Assert.Equal(goldBefore, sim.State.Gold);
        Assert.DoesNotContain(sim.Events.Span.ToArray(), e => e.Kind == EventKind.TowerRepaired);
    }

    // ---- the between-waves rule -------------------------------------------

    [Fact]
    public void WhileAWaveIsRunning_RepairIsRefused()
    {
        // The rule the whole slice turned on. Repair available during a wave
        // drove towers lost per run to exactly ZERO at every legal price: tower
        // destruction is throughput-driven, and an unlimited-rate counter beats
        // a throughput threat at any price the player can afford.
        (Sim sim, int towerId, _) = SimWithDamagedTower(1);
        sim.Enqueue(new StartWaveCommand());
        sim.Tick();
        Assert.True(sim.State.WaveActive);

        int goldBefore = sim.State.Gold;
        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        Assert.Equal(1, sim.State.TowerHp(0));
        Assert.Equal(goldBefore, sim.State.Gold);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.RepairRejected && e.A == (int)RejectReason.WaveInProgress);
    }

    [Fact]
    public void BetweenWaves_RepairIsAllowedAgain()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithDamagedTower(1);
        Assert.False(sim.State.WaveActive);

        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        Assert.Equal(def.Hp, sim.State.TowerHp(0));
    }

    // ---- the cost curve ---------------------------------------------------

    [Fact]
    public void RepairCost_ScalesWithHealthMissing()
    {
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");

        Assert.True(def.RepairCostFor(1, 10) < def.RepairCostFor(1, 90),
            "a barely-damaged tower must cost less to repair than a nearly-destroyed one");
    }

    [Fact]
    public void RepairingToFull_AlwaysBeatsSellingAndRebuilding()
    {
        // The upper wall. A player who does not repair can sell for half and
        // rebuild for full, a round trip whose net cost is exactly SellValueAt.
        // Above that line nobody ever repairs and the mechanic is decorative.
        foreach (TowerDef def in TestContent.BuildContent().Towers)
        {
            for (int level = 1; level <= def.MaxLevel; level++)
            {
                Assert.True(def.RepairCostFor(level, def.Hp) < def.SellValueAt(level),
                    $"{def.Id} at level {level}: repair {def.RepairCostFor(level, def.Hp)} " +
                    $"must be under sell-and-rebuild {def.SellValueAt(level)}");
            }
        }
    }

    [Fact]
    public void ManySmallRepairs_AreNeverCheaperThanOneLargeOne()
    {
        // Truncating division would make ten clicks cheaper than one, which is a
        // free heal for anyone willing to click. Rounding up closes it.
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");

        int oneGo = def.RepairCostFor(1, 50);
        int inTen = 0;
        for (int i = 0; i < 10; i++) inTen += def.RepairCostFor(1, 5);

        Assert.True(inTen >= oneGo, $"ten repairs of 5 cost {inTen}, one repair of 50 costs {oneGo}");
    }

    [Fact]
    public void RepairCost_RisesWithInvestment()
    {
        // Anchored to total spend, so concentrating gold into one upgraded tower
        // carries a maintenance liability proportional to the concentration.
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");

        Assert.True(def.RepairCostFor(3, def.Hp) > def.RepairCostFor(1, def.Hp));
    }

    [Fact]
    public void RepairCost_DoesNotOverflowAtExtremeValues()
    {
        // spent x percent x missingHp reaches ~1e14 here, far past int. Overflow
        // is exact and therefore deterministically WRONG, which the hash would
        // happily agree on across two machines.
        TowerDef[] towers = ContentLoader.LoadTowers(new[]
        {
            ("huge.json", """
            { "id": "huge", "name": "Huge", "cost": 1000000, "range": 1.0, "cooldown": 1.0,
              "damage": 1, "projectileSpeed": 1.0, "targeting": "nearest",
              "sellValue": 500000, "hp": 1000000, "repairPercent": 99 }
            """),
        });

        int cost = towers[0].RepairCostFor(1, 1_000_000);
        Assert.True(cost is > 0 and < 1_000_000, $"got {cost}");
        Assert.Equal(495_000, cost);
    }

    // ---- repair is orthogonal to upgrade ----------------------------------

    [Fact]
    public void RepairingDoesNotChangeTheLevel_AndUpgradingDoesNotHeal()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithDamagedTower(40);
        sim.MutableState.Gold = 100000;

        sim.Enqueue(new UpgradeCommand(towerId));
        sim.Tick();

        Assert.Equal(2, sim.State.TowerLevel(0));
        Assert.Equal(40, sim.State.TowerHp(0));      // upgrading is not a heal

        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        Assert.Equal(def.Hp, sim.State.TowerHp(0));
        Assert.Equal(2, sim.State.TowerLevel(0));    // repairing is not an upgrade
    }

    [Fact]
    public void RepairingAnUpgradedTower_CostsMoreThanRepairingABaseOne()
    {
        TowerDef def = TestContent.BuildContent().Towers.Single(t => t.Id == "arrow-tower");
        Assert.True(def.RepairCostFor(2, 50) > def.RepairCostFor(1, 50));
    }

    // ---- pathing ----------------------------------------------------------

    [Fact]
    public void ARepair_ChangesNoRoute()
    {
        // Same claim as upgrade: a repaired tower occupies the cell it already
        // occupied, so the grid never changes and phase 2 is never dirtied.
        (Sim sim, int towerId, _) = SimWithDamagedTower(1);
        ushort versionBefore = sim.Path.Version;

        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        Assert.Equal(versionBefore, sim.Path.Version);
        Assert.DoesNotContain(sim.Events.Span.ToArray(), e => e.Kind == EventKind.PathRecomputed);
    }

    // ---- determinism ------------------------------------------------------

    [Fact]
    public void Repair_IsDeterministicAcrossRuns()
    {
        Assert.Equal(RunOne(), RunOne());

        static ulong RunOne()
        {
            (Sim sim, int towerId, _) = SimWithDamagedTower(30);
            sim.Enqueue(new RepairCommand(towerId));
            for (int t = 0; t < 50; t++) sim.Tick();
            return sim.Hash();
        }
    }

    [Fact]
    public void SnapshotRestore_RoundTripsAPartiallyRepairedBoard()
    {
        (Sim sim, int towerId, TowerDef def) = SimWithDamagedTower(30);
        sim.Enqueue(new RepairCommand(towerId));
        sim.Tick();

        // Assert the fixture is genuinely mid-mechanic before trusting the round
        // trip -- a snapshot test over an untouched board proves nothing.
        Assert.Equal(def.Hp, sim.State.TowerHp(0));

        SimSnapshot snapshot = sim.Snapshot();
        ulong straightThrough = Advance(sim);

        sim.Restore(snapshot);
        Assert.Equal(straightThrough, Advance(sim));

        static ulong Advance(Sim s)
        {
            for (int t = 0; t < 40; t++) s.Tick();
            return s.Hash();
        }
    }

    // ---- the load-time bound (ADR-0007) -----------------------------------

    [Fact]
    public void ATowerWhoseRepairCostBeatsSellAndRebuild_FailsToLoad()
    {
        ContentException ex = Assert.Throws<ContentException>(() => ContentLoader.LoadTowers(new[]
        {
            ("dominated.json", """
            { "id": "dominated", "name": "Dominated", "cost": 100, "range": 1.0, "cooldown": 1.0,
              "damage": 1, "projectileSpeed": 1.0, "targeting": "nearest",
              "sellValue": 50, "hp": 100, "repairPercent": 99 }
            """),
        }));

        // A message that says only "invalid repair cost" is worse than no check:
        // the whole point is that the two numbers live in different places.
        Assert.Contains("dominated", ex.Message);
        Assert.Contains("level 1", ex.Message);
        Assert.Contains("99", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(150)]
    public void ARepairPercentOutsideOneToNinetyNine_FailsToLoad(int percent)
    {
        Assert.Throws<ContentException>(() => ContentLoader.LoadTowers(new[]
        {
            ($"bad.json", $$"""
            { "id": "bad", "name": "Bad", "cost": 100, "range": 1.0, "cooldown": 1.0,
              "damage": 1, "projectileSpeed": 1.0, "targeting": "nearest",
              "sellValue": 50, "hp": 100, "repairPercent": {{percent}} }
            """),
        }));
    }

    [Fact]
    public void EveryShippedTower_AuthorsItsRepairPercent()
    {
        // Criterion 15: the knob is data. A tower relying on the loader default
        // is a tower whose maintenance cost nobody chose -- so this reads the
        // shipped JSON rather than the fixture, which would pass on the default
        // and verify nothing.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");

        string towerDir = Path.Combine(dir!.FullName, "content-data", "towers");
        string[] files = Directory.GetFiles(towerDir, "*.json");
        Assert.NotEmpty(files);

        foreach (string file in files)
            Assert.Contains("\"repairPercent\"", File.ReadAllText(file));
    }
}
