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
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Enqueue(new StartWaveCommand());
        for (int t = 0; t < 60; t++) sim.Tick();
        Assert.True(sim.State.CreepCount > 0, "fixture must have live creeps");
        Assert.True(sim.State.TowerCount > 0, "fixture must have a tower");
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
    [Fact] public void Hash_Covers_Lives() => AssertHashChanges(s => s.Lives -= 1);
    [Fact] public void Hash_Covers_WaveIndex() => AssertHashChanges(s => s.WaveIndex += 1);
    [Fact] public void Hash_Covers_WaveActive() => AssertHashChanges(s => s.WaveActive = !s.WaveActive);
    [Fact] public void Hash_Covers_NextEntityId() => AssertHashChanges(s => s.NextEntityId += 1);

    [Fact] public void Hash_Covers_CreepHp() => AssertHashChanges(s => s.CreepHp[s.CreepSlotByOrder(0)] -= 1);
    [Fact] public void Hash_Covers_CreepCell() => AssertHashChanges(s => s.CreepCellIndex[s.CreepSlotByOrder(0)] += 1);
    [Fact] public void Hash_Covers_CreepProgress() =>
        AssertHashChanges(s => s.CreepProgress[s.CreepSlotByOrder(0)] += Core.Math.Fix32.FromFraction(1, 100));
    [Fact] public void Hash_Covers_CreepHeading() =>
        AssertHashChanges(s => s.CreepHeading[s.CreepSlotByOrder(0)] ^= 1);
    [Fact] public void Hash_Covers_CreepDefIndex() =>
        AssertHashChanges(s => s.CreepDefIndex[s.CreepSlotByOrder(0)] ^= 1);
    [Fact] public void Hash_Covers_CreepCount() => AssertHashChanges(s => s.RemoveCreepBySlot(s.CreepSlotByOrder(0)));

    [Fact] public void Hash_Covers_TowerCooldown() =>
        AssertHashChanges(s => s.TowerCooldown[s.TowerSlotByOrder(0)] += 1);
    [Fact] public void Hash_Covers_TowerCell() =>
        AssertHashChanges(s => s.TowerCellIndex[s.TowerSlotByOrder(0)] += 1);
    [Fact] public void Hash_Covers_TowerDefIndex() =>
        AssertHashChanges(s => s.TowerDefIndex[s.TowerSlotByOrder(0)] ^= 1);
    [Fact] public void Hash_Covers_TowerHp() =>
        AssertHashChanges(s => s.TowerHp[s.TowerSlotByOrder(0)] -= 1);
    [Fact] public void Hash_Covers_CreepAttackCooldown() =>
        AssertHashChanges(s => s.CreepAttackCooldown[s.CreepSlotByOrder(0)] += 1);

    [Fact] public void Hash_Covers_WaveEntrySpawned() => AssertHashChanges(s => s.WaveEntrySpawned[0] += 1);
    [Fact] public void Hash_Covers_WaveEntryNextTick() => AssertHashChanges(s => s.WaveEntryNextTick[0] += 1);

    [Fact]
    public void Hash_Covers_PathVersion_AndTheCostGrid()
    {
        Sim sim = SimWithEntities();
        ulong before = sim.Hash();
        sim.Enqueue(new BuildCommand(new GridCell(6, 5), sim.Content.TowerIndexOf("cannon")));
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
        // Kill a middle creep so a swap-remove reshuffles slots, then confirm two
        // runs that reached the same state by different removal orders agree.
        Sim a = SimWithEntities();
        Sim b = SimWithEntities();

        int[] idsA = Enumerable.Range(0, a.State.CreepCount).Select(a.State.CreepSlotByOrder)
            .Select(s => a.State.CreepId(s)).ToArray();
        Assert.True(idsA.Length >= 3, "need at least three creeps to reorder slots");

        // Remove the same two creeps, in opposite orders.
        a.MutableState.RemoveCreepBySlot(a.State.SlotOfCreep(idsA[0]));
        a.MutableState.RemoveCreepBySlot(a.State.SlotOfCreep(idsA[1]));
        b.MutableState.RemoveCreepBySlot(b.State.SlotOfCreep(idsA[1]));
        b.MutableState.RemoveCreepBySlot(b.State.SlotOfCreep(idsA[0]));

        Assert.Equal(a.Hash(), b.Hash());
    }
}
