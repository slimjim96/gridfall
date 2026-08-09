using Gridfall.Core;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// One test per hashed field. "I added it to the hash" is a claim; these are the
/// facts. A hash that misses a field is worse than no hash -- the harness reports
/// green while the game diverges (engine guide 04).
/// </summary>
public class HashCoverageTests
{
    private static Sim SimWithEntities()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 5);
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.StationIndexOf("arrow-station")));
        sim.Enqueue(new StartWaveCommand());
        for (int t = 0; t < 60; t++) sim.Tick();
        Assert.True(sim.State.VisitorCount > 0, "fixture must have live visitors");
        Assert.True(sim.State.StationCount > 0, "fixture must have a station");
        return sim;
    }

    private static void AssertHashChanges(Action<SimState> mutate)
    {
        Sim sim = SimWithEntities();
        ulong before = sim.Hash();
        mutate(sim.MutableState);
        Assert.NotEqual(before, sim.Hash());
    }

    [Fact] public void Hash_Covers_Gold() => AssertHashChanges(s => s.Gold += 1);
    [Fact] public void Hash_Covers_Patience() => AssertHashChanges(s => s.Patience -= 1);
    [Fact] public void Hash_Covers_WaveIndex() => AssertHashChanges(s => s.WaveIndex += 1);
    [Fact] public void Hash_Covers_WaveActive() => AssertHashChanges(s => s.WaveActive = !s.WaveActive);
    [Fact] public void Hash_Covers_NextEntityId() => AssertHashChanges(s => s.NextEntityId += 1);

    [Fact] public void Hash_Covers_VisitorAppetite() => AssertHashChanges(s => s.VisitorAppetite[s.VisitorSlotByOrder(0)] -= 1);
    [Fact] public void Hash_Covers_VisitorCell() => AssertHashChanges(s => s.VisitorCellIndex[s.VisitorSlotByOrder(0)] += 1);
    [Fact] public void Hash_Covers_VisitorProgress() =>
        AssertHashChanges(s => s.VisitorProgress[s.VisitorSlotByOrder(0)] += Core.Math.Fix32.FromFraction(1, 100));
    [Fact] public void Hash_Covers_VisitorHeading() =>
        AssertHashChanges(s => s.VisitorHeading[s.VisitorSlotByOrder(0)] ^= 1);
    [Fact] public void Hash_Covers_VisitorDefIndex() =>
        AssertHashChanges(s => s.VisitorDefIndex[s.VisitorSlotByOrder(0)] ^= 1);
    [Fact] public void Hash_Covers_VisitorCount() => AssertHashChanges(s => s.RemoveVisitorBySlot(s.VisitorSlotByOrder(0)));

    [Fact] public void Hash_Covers_StationCooldown() =>
        AssertHashChanges(s => s.StationCooldown[s.StationSlotByOrder(0)] += 1);
    [Fact] public void Hash_Covers_StationCell() =>
        AssertHashChanges(s => s.StationCellIndex[s.StationSlotByOrder(0)] += 1);
    [Fact] public void Hash_Covers_StationDefIndex() =>
        AssertHashChanges(s => s.StationDefIndex[s.StationSlotByOrder(0)] ^= 1);
    [Fact] public void Hash_Covers_StationStock() =>
        AssertHashChanges(s => s.StationStock[s.StationSlotByOrder(0)] -= 1);
    [Fact] public void Hash_Covers_VisitorAttackCooldown() =>
        AssertHashChanges(s => s.VisitorAttackCooldown[s.VisitorSlotByOrder(0)] += 1);

    [Fact] public void Hash_Covers_WaveEntrySpawned() => AssertHashChanges(s => s.WaveEntrySpawned[0] += 1);
    [Fact] public void Hash_Covers_WaveEntryNextTick() => AssertHashChanges(s => s.WaveEntryNextTick[0] += 1);

    [Fact]
    public void Hash_Covers_PathVersion_AndTheCostGrid()
    {
        Sim sim = SimWithEntities();
        ulong before = sim.Hash();
        sim.Enqueue(new BuildCommand(new GridCell(6, 5), sim.Content.StationIndexOf("cannon")));
        sim.Tick();
        Assert.NotEqual(before, sim.Hash());
    }

    [Fact]
    public void Hash_Covers_RandomPosition()
    {
        Sim sim = SimWithEntities();
        ulong before = sim.Hash();
        sim.Random.NextUInt64();
        Assert.NotEqual(before, sim.Hash());
    }

    [Fact]
    public void Hash_Covers_TickCount()
    {
        Sim a = TestContent.NewSim(TestContent.ArenaMap, seed: 5);
        ulong atZero = a.Hash();
        a.Tick();
        Assert.NotEqual(atZero, a.Hash());
    }

    [Fact]
    public void Hash_IsStableWhenNothingChanges()
    {
        Sim sim = SimWithEntities();
        Assert.Equal(sim.Hash(), sim.Hash());
    }

    [Fact]
    public void Hash_DoesNotDependOnSlotOrder()
    {
        // Kill a middle visitor so a swap-remove reshuffles slots, then confirm two
        // runs that reached the same state by different removal orders agree.
        Sim a = SimWithEntities();
        Sim b = SimWithEntities();

        int[] idsA = Enumerable.Range(0, a.State.VisitorCount).Select(a.State.VisitorSlotByOrder)
            .Select(s => a.State.VisitorId(s)).ToArray();
        Assert.True(idsA.Length >= 3, "need at least three visitors to reorder slots");

        // Remove the same two visitors, in opposite orders.
        a.MutableState.RemoveVisitorBySlot(a.State.SlotOfVisitor(idsA[0]));
        a.MutableState.RemoveVisitorBySlot(a.State.SlotOfVisitor(idsA[1]));
        b.MutableState.RemoveVisitorBySlot(b.State.SlotOfVisitor(idsA[1]));
        b.MutableState.RemoveVisitorBySlot(b.State.SlotOfVisitor(idsA[0]));

        Assert.Equal(a.Hash(), b.Hash());
    }
}
