using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Path;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The route overlay draws whatever TraceRoute returns, so the drawing is only
/// as trustworthy as this. Verified here rather than by looking at pips.
/// </summary>
public class RouteTraceTests
{
    private static (MapDef map, PathSystem path) Arena()
    {
        MapDef map = TestContent.Map(TestContent.ArenaMap);
        var sim = new Sim(map, TestContent.BuildContent(), 1);
        return (map, sim.Path);
    }

    [Fact]
    public void TraceRoute_ReachesTheGoal()
    {
        (MapDef map, PathSystem path) = Arena();
        var buffer = new int[4096];

        int count = path.TraceRoute(map.Index(map.Spawns[0]), buffer);

        Assert.True(count > 0);
        Assert.Equal(map.Index(map.Goal), buffer[count - 1]);
    }

    [Fact]
    public void TraceRoute_LengthMatchesTheDistanceField()
    {
        (MapDef map, PathSystem path) = Arena();
        var buffer = new int[4096];

        GridCell spawn = map.Spawns[0];
        int count = path.TraceRoute(map.Index(spawn), buffer);

        // distance is steps to the goal; the trace includes both endpoints.
        Assert.Equal(path.RouteLength(spawn) + 1, count);
    }

    [Fact]
    public void TraceRoute_StepsToAdjacentCellsOnly()
    {
        (MapDef map, PathSystem path) = Arena();
        var buffer = new int[4096];
        int count = path.TraceRoute(map.Index(map.Spawns[0]), buffer);

        for (int i = 1; i < count; i++)
        {
            int ax = buffer[i - 1] % map.Width, ay = buffer[i - 1] / map.Width;
            int bx = buffer[i] % map.Width, by = buffer[i] / map.Width;
            Assert.Equal(1, System.Math.Abs(ax - bx) + System.Math.Abs(ay - by));
        }
    }

    [Fact]
    public void TraceRoute_NeverExceedsTheCallersBuffer()
    {
        (MapDef map, PathSystem path) = Arena();
        var tiny = new int[3];
        int count = path.TraceRoute(map.Index(map.Spawns[0]), tiny);
        Assert.Equal(3, count);
    }

    [Fact]
    public void PreviewRoute_IsLongerAfterABuildThatLengthensIt()
    {
        (MapDef map, PathSystem path) = Arena();
        var live = new int[4096];
        var preview = new int[4096];

        int liveCount = path.TraceRoute(map.Index(map.Spawns[0]), live);

        // A cell squarely on the lane: blocking it must force a detour.
        int onTheLane = map.Index(new GridCell(5, 4));
        Assert.True(path.WouldRemainConnected(onTheLane), "fixture cell must leave the board connected");

        int previewCount = path.TraceRoute(map.Index(map.Spawns[0]), preview, preview: true);

        Assert.True(previewCount > liveCount,
            $"preview route ({previewCount}) should be longer than the live one ({liveCount})");
    }

    [Fact]
    public void WouldRemainConnected_AnswersConnectivity_NotBuildability()
    {
        // A distinction worth pinning down: this call says "the board stays
        // connected", NOT "you may build here". The view must check buildability
        // separately, and a test that conflated the two passed for the wrong
        // reason until this was noticed.
        (MapDef map, PathSystem path) = Arena();

        int pathOnly = map.Index(new GridCell(5, 4));
        Assert.Equal(CellKind.PathOnly, map.Cells[pathOnly]);
        Assert.True(path.WouldRemainConnected(pathOnly));   // connectivity: fine
                                                            // legality: not its question
    }

    [Fact]
    public void PreviewRoute_MatchesRealityAfterTheBuildLands()
    {
        // The whole promise of the drag preview: what you saw is what you get.
        MapDef map = TestContent.Map(TestContent.LaneMap);
        var sim = new Sim(map, TestContent.BuildContent(), 1);

        var cell = new GridCell(5, 3);
        int index = map.Index(cell);
        Assert.True(sim.Path.WouldRemainConnected(index));

        var predicted = new int[4096];
        int predictedCount = sim.Path.TraceRoute(map.Index(map.Spawns[0]), predicted, preview: true);

        sim.Enqueue(new BuildCommand(cell, sim.Content.StationIndexOf("arrow-station")));
        sim.Tick();

        var actual = new int[4096];
        int actualCount = sim.Path.TraceRoute(map.Index(map.Spawns[0]), actual);

        Assert.Equal(predictedCount, actualCount);
        Assert.Equal(predicted[..predictedCount], actual[..actualCount]);
    }

    [Fact]
    public void TraceRoute_TerminatesOnAnUnreachableCell()
    {
        // Defensive: a malformed field must cost a bounded walk, not a hang in
        // the render loop.
        MapDef map = TestContent.Map(TestContent.PinchMap);
        var sim = new Sim(map, TestContent.BuildContent(), 1);
        var buffer = new int[4096];

        // A walled-off cell the flow field never reached.
        int isolated = map.Index(new GridCell(1, 1));
        int count = sim.Path.TraceRoute(isolated, buffer);

        Assert.True(count >= 1 && count < buffer.Length);
    }

    [Fact]
    public void PathMutators_AreNotPubliclyReachable()
    {
        // Same boundary as SimStateView: the view reads pathing, never changes it.
        foreach (string name in new[] { "SetBlocked", "MarkDirty", "ForceRebuild", "RecomputeIfDirty", "RestoreFrom" })
            Assert.Null(typeof(PathSystem).GetMethod(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
    }
}
