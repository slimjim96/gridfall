using Gridfall.Core;
using Xunit;

namespace Gridfall.Tests;

public class DeterminismTests
{
    private static List<(int tick, ICommand cmd)> Script(Sim sim) => new()
    {
        (5,  new BuildCommand(new GridCell(4, 3), sim.Content.TowerIndexOf("arrow-tower"))),
        (10, new StartWaveCommand()),
        (40, new BuildCommand(new GridCell(6, 5), sim.Content.TowerIndexOf("cannon"))),
        (90, new BuildCommand(new GridCell(8, 3), sim.Content.TowerIndexOf("arrow-tower"))),
    };

    private static ulong[] Run(string mapJson, uint seed, int ticks)
    {
        Sim sim = TestContent.NewSim(mapJson, seed);
        List<(int tick, ICommand cmd)> script = Script(sim);
        var hashes = new ulong[ticks];

        for (int t = 0; t < ticks; t++)
        {
            foreach ((int at, ICommand cmd) in script)
                if (at == t) sim.Enqueue(cmd);
            sim.Tick();
            hashes[t] = sim.Hash();
        }
        return hashes;
    }

    [Fact]
    public void TwoIdenticalRuns_ProduceIdenticalHashes()
    {
        ulong[] a = Run(TestContent.ArenaMap, seed: 12345, ticks: 600);
        ulong[] b = Run(TestContent.ArenaMap, seed: 12345, ticks: 600);

        for (int t = 0; t < a.Length; t++)
            Assert.True(a[t] == b[t], $"diverged at tick {t}: {a[t]:x16} vs {b[t]:x16}");
    }

    [Fact]
    public void TheRunActuallyDoesSomething()
    {
        // Guards the test above: a sim that never changes state hashes
        // identically forever and proves nothing.
        ulong[] a = Run(TestContent.ArenaMap, seed: 1, ticks: 600);
        Assert.True(a.Distinct().Count() > 50,
            "Too few distinct hashes -- the determinism test may be passing vacuously.");
    }

    [Fact]
    public void SnapshotRoundTrip_MatchesRunningStraightThrough()
    {
        Sim direct = TestContent.NewSim(TestContent.ArenaMap, seed: 7);
        Sim viaSnapshot = TestContent.NewSim(TestContent.ArenaMap, seed: 7);

        foreach (Sim s in new[] { direct, viaSnapshot })
        {
            s.Enqueue(new BuildCommand(new GridCell(4, 3), s.Content.TowerIndexOf("arrow-tower")));
            s.Enqueue(new StartWaveCommand());
        }

        for (int t = 0; t < 120; t++) { direct.Tick(); viaSnapshot.Tick(); }

        SimSnapshot snap = viaSnapshot.Snapshot();
        for (int t = 0; t < 60; t++) viaSnapshot.Tick();   // diverge deliberately
        viaSnapshot.Restore(snap);

        for (int t = 0; t < 200; t++)
        {
            direct.Tick();
            viaSnapshot.Tick();
            Assert.True(direct.Hash() == viaSnapshot.Hash(),
                $"restore diverged {t} ticks after the round trip");
        }
    }

    [Fact]
    public void SameState_ProducesSameEvents()
    {
        Sim a = TestContent.NewSim(TestContent.ArenaMap, seed: 3);
        Sim b = TestContent.NewSim(TestContent.ArenaMap, seed: 3);
        a.Enqueue(new StartWaveCommand());
        b.Enqueue(new StartWaveCommand());

        for (int t = 0; t < 400; t++)
        {
            a.Tick();
            b.Tick();
            Assert.Equal(a.Events.Count, b.Events.Count);
            for (int i = 0; i < a.Events.Count; i++)
            {
                Assert.Equal(a.Events[i].Kind, b.Events[i].Kind);
                Assert.Equal(a.Events[i].A, b.Events[i].A);
                Assert.Equal(a.Events[i].B, b.Events[i].B);
            }
        }
    }

    [Fact]
    public void CreepRoute_IsIdenticalAcrossRuns()
    {
        static List<int> RouteOfFirstCreep(uint seed)
        {
            Sim sim = TestContent.NewSim(TestContent.DoglegTieMap, seed);
            sim.Enqueue(new StartWaveCommand());
            var route = new List<int>();

            for (int t = 0; t < 900; t++)
            {
                sim.Tick();
                int slot = sim.State.SlotOfCreep(1);   // first spawned entity
                if (slot < 0) continue;
                int cell = sim.State.CreepCellIndex[slot];
                if (route.Count == 0 || route[^1] != cell) route.Add(cell);
            }
            return route;
        }

        List<int> first = RouteOfFirstCreep(1);
        Assert.NotEmpty(first);
        for (uint seed = 2; seed <= 50; seed++)
            Assert.Equal(first, RouteOfFirstCreep(seed));
    }
}
