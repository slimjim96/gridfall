using Gridfall.Core;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Xunit;

namespace Gridfall.Tests;

public class SimulationTests
{
    [Fact]
    public void AWave_RunsStartToFinish()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new StartWaveCommand());

        int spawned = 0, died = 0, leaked = 0;
        bool cleared = false;

        for (int t = 0; t < 2000 && !cleared; t++)
        {
            sim.Tick();
            foreach (SimEvent e in sim.Events.Span)
            {
                switch (e.Kind)
                {
                    case EventKind.CreepSpawned: spawned++; break;
                    case EventKind.CreepDied: died++; break;
                    case EventKind.CreepLeaked: leaked++; break;
                    case EventKind.WaveCleared: cleared = true; break;
                }
            }
        }

        Assert.True(cleared, "wave never cleared");
        Assert.Equal(4, spawned);                 // wave 1 of the test table
        Assert.Equal(4, died + leaked);           // every creep resolved exactly once
        Assert.Equal(0, sim.State.CreepCount);
    }

    [Fact]
    public void UndefendedCreeps_LeakAndCostLives()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        int livesBefore = sim.State.Lives;
        sim.Enqueue(new StartWaveCommand());

        for (int t = 0; t < 2000; t++) sim.Tick();

        Assert.True(sim.State.Lives < livesBefore, "no lives lost with no towers built");
        Assert.Equal(0, sim.State.CreepCount);
    }

    [Fact]
    public void ATower_KillsCreepsAndEarnsBounty()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        int startingGold = sim.State.Gold;
        const int sniperCost = 10;

        sim.Enqueue(new BuildCommand(new GridCell(5, 3), sim.Content.TowerIndexOf("sniper")));
        sim.Enqueue(new StartWaveCommand());

        int deaths = 0;
        for (int t = 0; t < 2000; t++)
        {
            sim.Tick();
            foreach (SimEvent e in sim.Events.Span)
                if (e.Kind == EventKind.CreepDied) deaths++;
        }

        Assert.Equal(4, deaths);
        Assert.Equal(TestContent.Map(TestContent.ArenaMap).StartingLives, sim.State.Lives);
        Assert.Equal(startingGold - sniperCost + 4 * 8, sim.State.Gold);   // four runners at 8 bounty
    }

    [Fact]
    public void AWave_SpawnsOnTheTickItStarts()
    {
        // StartWave is applied in phase 1 and SpawnSystem runs in phase 3, so an
        // entry with no delay spawns on the same tick. Worth pinning down: it
        // surprised a test that assumed a tick of grace.
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new StartWaveCommand());
        sim.Tick();

        Assert.Equal(1, sim.State.CreepCount);
        Assert.Contains(sim.Events.Span.ToArray(), e => e.Kind == EventKind.CreepSpawned);
    }

    [Fact]
    public void TwoTowersKillingTheSameCreep_ProduceOneDeathAndOneBounty()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        ushort sniper = sim.Content.TowerIndexOf("sniper");

        // Two snipers, same cooldown, both in range of the lane: they fire on the
        // same tick at the same target, and each shot is individually lethal.
        sim.Enqueue(new BuildCommand(new GridCell(5, 3), sniper));
        sim.Enqueue(new BuildCommand(new GridCell(5, 5), sniper));
        sim.Enqueue(new StartWaveCommand());

        int deaths = 0, goldGained = 0;
        for (int t = 0; t < 2000; t++)
        {
            sim.Tick();
            foreach (SimEvent e in sim.Events.Span)
            {
                if (e.Kind == EventKind.CreepDied) deaths++;
                if (e.Kind == EventKind.GoldChanged && e.B > 0) goldGained += e.B;
            }
        }

        Assert.Equal(4, deaths);              // four creeps, four deaths -- not eight
        Assert.Equal(4 * 8, goldGained);      // and four bounties
    }

    [Fact]
    public void ACreep_FinishesCrossingACellBeforeTurning()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new StartWaveCommand());

        // Advance until the first creep is mid-cell.
        int slot = -1;
        for (int t = 0; t < 200; t++)
        {
            sim.Tick();
            slot = sim.State.SlotOfCreep(1);
            if (slot >= 0 && sim.State.CreepProgress[slot] > Fix32.FromFraction(3, 10)) break;
        }
        Assert.True(slot >= 0, "no creep to observe");

        byte headingBefore = sim.State.CreepHeading[slot];
        int cellBefore = sim.State.CreepCellIndex[slot];

        // Change the maze under it, mid-crossing.
        sim.Enqueue(new BuildCommand(new GridCell(8, 3), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Tick();

        slot = sim.State.SlotOfCreep(1);
        if (sim.State.CreepCellIndex[slot] == cellBefore)
            Assert.Equal(headingBefore, sim.State.CreepHeading[slot]);
    }

    [Fact]
    public void SellingATower_RefundsAndUnblocksTheCell()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        ushort arrow = sim.Content.TowerIndexOf("arrow-tower");

        int goldBefore = sim.State.Gold;
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), arrow));
        sim.Tick();

        int towerId = sim.State.TowerId[0];
        Assert.Equal(goldBefore - 50, sim.State.Gold);
        Assert.True(sim.Path.IsBlocked(sim.Map.Index(new GridCell(4, 3))));

        sim.Enqueue(new SellCommand(towerId));
        sim.Tick();

        Assert.Equal(0, sim.State.TowerCount);
        Assert.Equal(goldBefore - 50 + 25, sim.State.Gold);
        Assert.False(sim.Path.IsBlocked(sim.Map.Index(new GridCell(4, 3))));
    }

    [Fact]
    public void BuildingWithoutEnoughGold_IsRefused()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.State.Gold = 10;

        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Tick();

        Assert.Equal(0, sim.State.TowerCount);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.BuildRejected && e.A == (int)RejectReason.InsufficientGold);
    }

    [Fact]
    public void BuildingOnANonBuildableCell_IsRefused()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);

        sim.Enqueue(new BuildCommand(new GridCell(5, 4), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Tick();

        Assert.Equal(0, sim.State.TowerCount);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.BuildRejected && e.A == (int)RejectReason.NotBuildable);
    }

    [Fact]
    public void EntityIds_AreNeverReusedWithinARun()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new StartWaveCommand());

        var seen = new HashSet<int>();
        for (int t = 0; t < 2000; t++)
        {
            sim.Tick();
            foreach (SimEvent e in sim.Events.Span)
                if (e.Kind == EventKind.CreepSpawned)
                    Assert.True(seen.Add(e.A), $"entity id {e.A} reused");
        }
    }

    [Fact]
    public void TheTickLoop_DoesNotAllocateInSteadyState()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 1);
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Enqueue(new StartWaveCommand());
        for (int t = 0; t < 300; t++) sim.Tick();   // let buffers reach their steady size

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int t = 0; t < 1000; t++) sim.Tick();

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated == 0, $"tick loop allocated {allocated} bytes over 1000 ticks");
    }
}
