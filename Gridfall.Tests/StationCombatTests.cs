using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Visitors that attack stations while walking, and stations that can be destroyed
/// (ADR-0006).
///
/// This mechanic exists to break an invariant seven balance passes ran into:
/// total defence tracks cumulative income, which holds only because stations are
/// permanent. So the tests that matter here are not "serving is subtracted" --
/// they are the ordering and determinism guarantees that let the balance
/// numbers mean anything.
/// </summary>
public class StationCombatTests
{
    // Range 1.6 reaches a station one cell off the lane and nothing further.
    private const string Sapper = """
    { "id": "sapper", "name": "Sapper", "appetite": 400, "speed": 0.04, "bounty": 5,
      "attackDrain": 10, "attackCooldown": 1.0, "attackRange": 1.6 }
    """;

    /// <summary>Walks the same lane and never attacks. The control.</summary>
    private const string Passer = """
    { "id": "passer", "name": "Passer", "appetite": 400, "speed": 0.04, "bounty": 5 }
    """;

    private static string OneOf(string visitor) => $$"""
    { "map": "t", "waves": [ { "index": 1, "entries": [
        { "visitor": "{{visitor}}", "count": 1, "spacingTicks": 10 } ] } ] }
    """;

    /// <summary>
    /// A station with no offence at all, so nothing here depends on whether the
    /// station manages to kill the attacker first.
    /// </summary>
    private static string Post(int hp) => $$"""
    { "id": "post", "name": "Post", "cost": 10, "range": 0.1, "cooldown": 99.0,
      "serving": 0, "projectileSpeed": 1.0, "stock": {{hp}},
      "targeting": "furthest-along-path", "sellValue": 5 }
    """;

    private static ContentSet Content(int stationStock, string visitorJson, string visitorId)
    {
        StationDef[] stations = ContentLoader.LoadStations(new[] { ("post.json", Post(stationStock)) });
        VisitorDef[] visitors = ContentLoader.LoadVisitors(new[] { ($"{visitorId}.json", visitorJson) });
        return new ContentSet
        {
            Stations = stations,
            Visitors = visitors,
            Waves = ContentLoader.LoadWaves(OneOf(visitorId), visitors, "w.json"),
        };
    }

    /// <summary>Arena lane runs along y=4; (5,3) is one cell off it.</summary>
    private static Sim Fixture(int stationStock, string visitorJson = Sapper, string visitorId = "sapper")
    {
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), Content(stationStock, visitorJson, visitorId), 1);
        sim.Enqueue(new BuildCommand(new GridCell(5, 3), sim.Content.StationIndexOf("post")));
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
    public void AnVisitorWithNoAttackDrain_NeverTouchesAStation()
    {
        // The default. Every visitor that shipped before this slice must be
        // unaffected, or the balance history stops being comparable.
        Sim sim = Fixture(200, Passer, "passer");
        List<SimEvent> events = RunFor(sim, 400);

        Assert.DoesNotContain(events, e => e.Kind == EventKind.StationDepleted);
        Assert.Equal(200, sim.State.StationStock(sim.State.StationSlotByOrder(0)));
    }

    [Fact]
    public void ASapperServingsAStationItWalksPast()
    {
        Sim sim = Fixture(10_000);
        List<SimEvent> events = RunFor(sim, 400);

        Assert.Contains(events, e => e.Kind == EventKind.StationDepleted);
        Assert.True(sim.State.StationStock(sim.State.StationSlotByOrder(0)) < 10_000,
            "the station should have taken serving on the way past");
    }

    [Fact]
    public void ASapperKeepsWalkingWhileItAttacks()
    {
        // "Attack while walking" is the design decision, not an implementation
        // detail: a visitor that stopped to attack would be trivially kited and
        // would change wave timing everywhere.
        Sim sim = Fixture(10_000);

        int slot = -1;
        int cellAtFirstHit = -1, cellLater = -1;

        for (int t = 0; t < 400; t++)
        {
            sim.Tick();
            bool hit = false;
            foreach (SimEvent e in sim.Events.Span)
                if (e.Kind == EventKind.StationDepleted) hit = true;

            if (sim.State.VisitorCount == 0) continue;
            slot = sim.State.VisitorSlotByOrder(0);

            if (hit && cellAtFirstHit < 0) cellAtFirstHit = sim.State.VisitorCellIndex(slot);
            else if (cellAtFirstHit >= 0) cellLater = sim.State.VisitorCellIndex(slot);
        }

        Assert.True(cellAtFirstHit >= 0, "fixture never produced a hit");
        Assert.NotEqual(cellAtFirstHit, cellLater);
    }

    [Fact]
    public void AStationReducedToZero_IsDestroyedAndAnnounced()
    {
        Sim sim = Fixture(20);
        List<SimEvent> events = RunFor(sim, 400);

        Assert.Contains(events, e => e.Kind == EventKind.StationDestroyed);
        Assert.Equal(0, sim.State.StationCount);
    }

    [Fact]
    public void ADestroyedStation_UnblocksItsCellForPathing()
    {
        // The consequence ADR-0006 calls out. A station that dies but leaves its
        // cell blocked is a permanent invisible wall.
        Sim sim = Fixture(20);

        // One tick first: a command queued is not a command applied, and phase 1
        // runs on the NEXT tick. Reading the slot before that reads cell 0.
        sim.Tick();
        Assert.Equal(1, sim.State.StationCount);
        int cell = sim.State.StationCellIndex(sim.State.StationSlotByOrder(0));

        RunFor(sim, 400);

        Assert.Equal(0, sim.State.StationCount);
        Assert.False(sim.Path.IsBlocked(cell),
            "the cell a destroyed station stood on must stop blocking the flow field");

        // And provably rebuildable, not merely unblocked in the cost grid.
        sim.Enqueue(new BuildCommand(new GridCell(cell % 12, cell / 12),
            sim.Content.StationIndexOf("post")));
        sim.Tick();
        Assert.Equal(1, sim.State.StationCount);
    }

    [Fact]
    public void AttacksAreRateLimitedByCooldown()
    {
        // Throughput, not serving per hit, is what the tuning sweep turned out to
        // be sensitive to -- so the cooldown is load-bearing and gets a test.
        Sim sim = Fixture(10_000);
        List<SimEvent> events = RunFor(sim, 400);

        int hits = events.Count(e => e.Kind == EventKind.StationDepleted);
        int serving = 10_000 - sim.State.StationStock(sim.State.StationSlotByOrder(0));

        Assert.Equal(hits * 10, serving);
        Assert.True(hits < 400 / 30, $"{hits} hits in 400 ticks ignores a 1.0s cooldown");
    }

    // ---- determinism ------------------------------------------------------

    [Fact]
    public void StationDrain_IsDeterministicAcrossRuns()
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
        // StationStock and VisitorAttackCooldown are new state. State that is hashed
        // but not snapshotted diverges only after a save/load, which is the
        // hardest kind of divergence to find (engine guide 04).
        Sim sim = Fixture(10_000);
        for (int t = 0; t < 200; t++) sim.Tick();

        Assert.True(sim.State.StationStock(sim.State.StationSlotByOrder(0)) < 10_000,
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
