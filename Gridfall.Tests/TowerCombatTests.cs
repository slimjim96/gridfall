using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Enemies that attack towers while walking, and towers that can be destroyed
/// (ADR-0006).
///
/// This mechanic exists to break an invariant seven balance passes ran into:
/// total defence tracks cumulative income, which holds only because towers are
/// permanent. So the tests that matter here are not "damage is subtracted" --
/// they are the ordering and determinism guarantees that let the balance
/// numbers mean anything.
/// </summary>
public class TowerCombatTests
{
    // Range 1.6 reaches a tower one cell off the lane and nothing further.
    private const string Sapper = """
    { "id": "sapper", "name": "Sapper", "hp": 400, "speed": 0.04, "bounty": 5,
      "attackDamage": 10, "attackCooldown": 1.0, "attackRange": 1.6 }
    """;

    /// <summary>Walks the same lane and never attacks. The control.</summary>
    private const string Passer = """
    { "id": "passer", "name": "Passer", "hp": 400, "speed": 0.04, "bounty": 5 }
    """;

    private static string OneOf(string enemy) => $$"""
    { "map": "t", "waves": [ { "index": 1, "entries": [
        { "enemy": "{{enemy}}", "count": 1, "spacingTicks": 10 } ] } ] }
    """;

    /// <summary>
    /// A tower with no offence at all, so nothing here depends on whether the
    /// tower manages to kill the attacker first.
    /// </summary>
    private static string Post(int hp) => $$"""
    { "id": "post", "name": "Post", "cost": 10, "range": 0.1, "cooldown": 99.0,
      "damage": 0, "projectileSpeed": 1.0, "hp": {{hp}},
      "targeting": "furthest-along-path", "sellValue": 5 }
    """;

    private static ContentSet Content(int towerHp, string enemyJson, string enemyId)
    {
        TowerDef[] towers = ContentLoader.LoadTowers(new[] { ("post.json", Post(towerHp)) });
        EnemyDef[] enemies = ContentLoader.LoadEnemies(new[] { ($"{enemyId}.json", enemyJson) });
        return new ContentSet
        {
            Towers = towers,
            Enemies = enemies,
            Waves = ContentLoader.LoadWaves(OneOf(enemyId), enemies, "w.json"),
        };
    }

    /// <summary>Arena lane runs along y=4; (5,3) is one cell off it.</summary>
    private static Sim Fixture(int towerHp, string enemyJson = Sapper, string enemyId = "sapper")
    {
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), Content(towerHp, enemyJson, enemyId), 1);
        sim.Enqueue(new BuildCommand(new GridCell(5, 3), sim.Content.TowerIndexOf("post")));
        sim.Enqueue(new StartWaveCommand());
        return sim;
    }

    private static List<SimEvent> RunFor(Sim sim, int ticks)
    {
        var seen = new List<SimEvent>();
        for (int t = 0; t < ticks; t++)
        {
            sim.Tick();
            seen.AddRange(sim.Events.Span.ToArray());
        }
        return seen;
    }

    // ---- the mechanic -----------------------------------------------------

    [Fact]
    public void AnEnemyWithNoAttackDamage_NeverTouchesATower()
    {
        // The default. Every enemy that shipped before this slice must be
        // unaffected, or the balance history stops being comparable.
        Sim sim = Fixture(200, Passer, "passer");
        List<SimEvent> events = RunFor(sim, 400);

        Assert.DoesNotContain(events, e => e.Kind == EventKind.TowerDamaged);
        Assert.Equal(200, sim.State.TowerHp(sim.State.TowerSlotByOrder(0)));
    }

    [Fact]
    public void ASapperDamagesATowerItWalksPast()
    {
        Sim sim = Fixture(10_000);
        List<SimEvent> events = RunFor(sim, 400);

        Assert.Contains(events, e => e.Kind == EventKind.TowerDamaged);
        Assert.True(sim.State.TowerHp(sim.State.TowerSlotByOrder(0)) < 10_000,
            "the tower should have taken damage on the way past");
    }

    [Fact]
    public void ASapperKeepsWalkingWhileItAttacks()
    {
        // "Attack while walking" is the design decision, not an implementation
        // detail: a creep that stopped to attack would be trivially kited and
        // would change wave timing everywhere.
        Sim sim = Fixture(10_000);

        int slot = -1;
        int cellAtFirstHit = -1, cellLater = -1;

        for (int t = 0; t < 400; t++)
        {
            sim.Tick();
            bool hit = false;
            foreach (SimEvent e in sim.Events.Span)
                if (e.Kind == EventKind.TowerDamaged) hit = true;

            if (sim.State.CreepCount == 0) continue;
            slot = sim.State.CreepSlotByOrder(0);

            if (hit && cellAtFirstHit < 0) cellAtFirstHit = sim.State.CreepCellIndex(slot);
            else if (cellAtFirstHit >= 0) cellLater = sim.State.CreepCellIndex(slot);
        }

        Assert.True(cellAtFirstHit >= 0, "fixture never produced a hit");
        Assert.NotEqual(cellAtFirstHit, cellLater);
    }

    [Fact]
    public void ATowerReducedToZero_IsDestroyedAndAnnounced()
    {
        Sim sim = Fixture(20);
        List<SimEvent> events = RunFor(sim, 400);

        Assert.Contains(events, e => e.Kind == EventKind.TowerDestroyed);
        Assert.Equal(0, sim.State.TowerCount);
    }

    [Fact]
    public void ADestroyedTower_UnblocksItsCellForPathing()
    {
        // The consequence ADR-0006 calls out. A tower that dies but leaves its
        // cell blocked is a permanent invisible wall.
        Sim sim = Fixture(20);

        // One tick first: a command queued is not a command applied, and phase 1
        // runs on the NEXT tick. Reading the slot before that reads cell 0.
        sim.Tick();
        Assert.Equal(1, sim.State.TowerCount);
        int cell = sim.State.TowerCellIndex(sim.State.TowerSlotByOrder(0));

        RunFor(sim, 400);

        Assert.Equal(0, sim.State.TowerCount);
        Assert.False(sim.Path.IsBlocked(cell),
            "the cell a destroyed tower stood on must stop blocking the flow field");

        // And provably rebuildable, not merely unblocked in the cost grid.
        sim.Enqueue(new BuildCommand(new GridCell(cell % 12, cell / 12),
            sim.Content.TowerIndexOf("post")));
        sim.Tick();
        Assert.Equal(1, sim.State.TowerCount);
    }

    [Fact]
    public void AttacksAreRateLimitedByCooldown()
    {
        // Throughput, not damage per hit, is what the tuning sweep turned out to
        // be sensitive to -- so the cooldown is load-bearing and gets a test.
        Sim sim = Fixture(10_000);
        List<SimEvent> events = RunFor(sim, 400);

        int hits = events.Count(e => e.Kind == EventKind.TowerDamaged);
        int damage = 10_000 - sim.State.TowerHp(sim.State.TowerSlotByOrder(0));

        Assert.Equal(hits * 10, damage);
        Assert.True(hits < 400 / 30, $"{hits} hits in 400 ticks ignores a 1.0s cooldown");
    }

    // ---- determinism ------------------------------------------------------

    [Fact]
    public void TowerDamage_IsDeterministicAcrossRuns()
    {
        ulong Run()
        {
            Sim sim = Fixture(150);
            for (int t = 0; t < 400; t++) sim.Tick();
            return sim.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void SnapshotRestore_RoundTripsMidAttack()
    {
        // TowerHp and CreepAttackCooldown are new state. State that is hashed
        // but not snapshotted diverges only after a save/load, which is the
        // hardest kind of divergence to find (engine guide 04).
        Sim sim = Fixture(10_000);
        for (int t = 0; t < 200; t++) sim.Tick();

        Assert.True(sim.State.TowerHp(sim.State.TowerSlotByOrder(0)) < 10_000,
            "fixture must be mid-attack for this to prove anything");

        SimSnapshot snap = sim.Snapshot();
        ulong before = sim.Hash();

        Sim restored = Fixture(10_000);
        restored.Restore(snap);

        Assert.Equal(before, restored.Hash());

        for (int t = 0; t < 100; t++) { sim.Tick(); restored.Tick(); }
        Assert.Equal(sim.Hash(), restored.Hash());
    }
}
