using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Path;
using Xunit;

namespace Gridfall.Tests;

public class PathingTests
{
    /// <summary>
    /// A reference flow field, with a switch for the exact defect the worked
    /// example describes: overwriting a cell's direction when it is reached
    /// again at EQUAL distance, instead of leaving the first assignment alone.
    /// </summary>
    private static (ushort[] dist, byte[] flow) Reference(MapDef map, bool overwriteOnEqual)
    {
        int n = map.Width * map.Height;
        var dist = new ushort[n];
        var flow = new byte[n];
        Array.Fill(dist, PathSystem.NoDistance);
        Array.Fill(flow, PathSystem.Unreachable);

        var queue = new Queue<int>();
        int goal = map.Index(map.Goal);
        dist[goal] = 0;
        flow[goal] = PathSystem.GoalMarker;
        queue.Enqueue(goal);

        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            int cx = cell % map.Width, cy = cell / map.Width;
            var next = (ushort)(dist[cell] + 1);

            for (int d = 0; d < 4; d++)
            {
                (int dx, int dy) = Directions.Offsets[d];
                int nx = cx + dx, ny = cy + dy;
                if (!map.InBounds(nx, ny)) continue;
                int idx = ny * map.Width + nx;
                if (map.Cells[idx] == CellKind.Blocked) continue;

                if (dist[idx] == PathSystem.NoDistance)
                {
                    dist[idx] = next;
                    flow[idx] = Directions.Opposite((byte)d);
                    queue.Enqueue(idx);
                }
                else if (overwriteOnEqual && dist[idx] == next)
                {
                    flow[idx] = Directions.Opposite((byte)d);   // the defect
                }
            }
        }
        return (dist, flow);
    }

    [Fact]
    public void FlowField_MatchesFirstAssignmentWinsReference()
    {
        MapDef map = TestContent.Map(TestContent.ArenaMap);
        var path = new PathSystem(map);
        path.ForceRebuild();

        var (dist, flow) = Reference(map, overwriteOnEqual: false);

        for (int i = 0; i < map.Width * map.Height; i++)
        {
            if (map.Cells[i] == CellKind.Blocked) continue;
            Assert.Equal(dist[i], path.DistanceAt(i));
            Assert.Equal(flow[i], path.FlowAt(i));
        }
    }

    /// <summary>
    /// Guards the test above. If the fixture cannot tell the correct rule from
    /// the defect, the test proves nothing -- which is exactly how the bug hid
    /// behind a mirror-symmetric map in the worked example.
    /// </summary>
    [Fact]
    public void ArenaFixture_ActuallyDistinguishesTheTieBreakDefect()
    {
        MapDef map = TestContent.Map(TestContent.ArenaMap);
        var (_, correct) = Reference(map, overwriteOnEqual: false);
        var (_, defective) = Reference(map, overwriteOnEqual: true);

        Assert.False(correct.AsSpan().SequenceEqual(defective),
            "This fixture produces the same field either way, so the tie-break test is vacuous. " +
            "Pick a map with converging equal-cost routes.");
    }

    [Fact]
    public void FlowField_IsIdenticalAcrossIndependentBuilds()
    {
        MapDef map = TestContent.Map(TestContent.ArenaMap);
        var a = new PathSystem(map);
        var b = new PathSystem(map);
        a.ForceRebuild();
        b.ForceRebuild();

        for (int i = 0; i < map.Width * map.Height; i++)
        {
            Assert.Equal(a.FlowAt(i), b.FlowAt(i));
            Assert.Equal(a.DistanceAt(i), b.DistanceAt(i));
        }
    }

    [Fact]
    public void EveryFlowDirection_PointsToAStrictlyCloserCell()
    {
        MapDef map = TestContent.Map(TestContent.ArenaMap);
        var path = new PathSystem(map);
        path.ForceRebuild();

        for (int i = 0; i < map.Width * map.Height; i++)
        {
            byte flow = path.FlowAt(i);
            if (flow is PathSystem.Unreachable or PathSystem.GoalMarker) continue;

            (int dx, int dy) = Directions.Offsets[flow];
            int nx = i % map.Width + dx, ny = i / map.Width + dy;
            Assert.True(map.InBounds(nx, ny));
            Assert.Equal(path.DistanceAt(i) - 1, path.DistanceAt(ny * map.Width + nx));
        }
    }

    [Fact]
    public void Build_ThatWouldSealTheLane_IsRefusedAndLeavesTheGridUnchanged()
    {
        Sim sim = TestContent.NewSim(TestContent.PinchMap);
        ushort towerDef = sim.Content.TowerIndexOf("arrow-tower");

        byte[] costBefore = sim.Path.CostSpan.ToArray();
        int goldBefore = sim.State.Gold;

        sim.Enqueue(new BuildCommand(new GridCell(5, 3), towerDef));
        sim.Tick();

        Assert.Equal(0, sim.State.TowerCount);
        Assert.Equal(goldBefore, sim.State.Gold);
        Assert.True(costBefore.AsSpan().SequenceEqual(sim.Path.CostSpan));

        var rejected = sim.Events.Span.ToArray()
            .Single(e => e.Kind == Core.Events.EventKind.BuildRejected);
        Assert.Equal((int)Core.Events.RejectReason.WouldSealLane, rejected.A);
    }

    [Fact]
    public void Build_ThatOnlyLengthensTheRoute_IsAccepted()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap);
        ushort towerDef = sim.Content.TowerIndexOf("arrow-tower");

        sim.Enqueue(new BuildCommand(new GridCell(4, 3), towerDef));
        sim.Tick();

        Assert.Equal(1, sim.State.TowerCount);
        Assert.Contains(sim.Events.Span.ToArray(), e => e.Kind == Core.Events.EventKind.BuildPlaced);
    }

    [Fact]
    public void PathVersion_IncrementsOnlyWhenTheGridChanged()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap);
        ushort version = sim.Path.Version;

        sim.Tick();
        Assert.Equal(version, sim.Path.Version);   // nothing changed: no rebuild

        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.TowerIndexOf("arrow-tower")));
        sim.Tick();
        Assert.Equal(version + 1, sim.Path.Version);

        sim.Tick();
        Assert.Equal(version + 1, sim.Path.Version);   // and back to free
    }

    [Fact]
    public void UnreachableGoal_FailsAtLoad()
    {
        const string sealed_ = """
        {
          "id": "sealed", "width": 12, "height": 8,
          "cells": [
            "############",
            "#####.######",
            "#####.######",
            "S####.#####G",
            "#####.######",
            "#####.######",
            "############",
            "############"
          ],
          "spawns": [{ "x": 0, "y": 3 }]
        }
        """;

        ContentException ex = Assert.Throws<ContentException>(
            () => TestContent.Map(sealed_, "sealed.json"));
        Assert.Contains("cannot reach the goal", ex.Message);
    }
}
