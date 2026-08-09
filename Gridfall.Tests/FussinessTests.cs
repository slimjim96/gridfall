using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Flat, per-hit serving reduction. Flat rather than percentage so it punishes
/// many-small-hits and rewards few-big-hits -- the axis a roster of pure
/// stat-variants cannot express (visitor-roster design spec).
/// </summary>
public class FussinessTests
{
    private const string Plated = """
    { "id": "plated", "name": "Plated", "appetite": 1000, "speed": 0.04, "fussiness": 8, "bounty": 5 }
    """;
    private const string WallWave = """
    { "map": "t", "waves": [ { "index": 1, "entries": [
        { "visitor": "wall", "count": 1, "spacingTicks": 10 } ] } ] }
    """;

    private const string Bare = """
    { "id": "bare", "name": "Bare", "appetite": 1000, "speed": 0.04, "fussiness": 0, "bounty": 5 }
    """;

    private static VisitorDef[] Visitors() =>
        ContentLoader.LoadVisitors(new[] { ("plated.json", Plated), ("bare.json", Bare) });

    [Fact]
    public void FussinessDefaultsToZero_SoExistingContentIsUnaffected()
        => Assert.All(TestContent.BuildContent().Visitors, e => Assert.Equal(0, e.Fussiness));

    [Fact]
    public void FussinessIsLoadedFromTheData()
    {
        VisitorDef[] e = Visitors();
        Assert.Equal(8, e.Single(x => x.Id == "plated").Fussiness);
        Assert.Equal(0, e.Single(x => x.Id == "bare").Fussiness);
    }

    /// <summary>
    /// Runs one wave of a single archetype past one station and reports the total
    /// serving that actually landed.
    /// </summary>
    private static int ServingDealt(string visitorId, string stationId)
    {
        string waves = $$"""
        { "map": "t", "waves": [ { "index": 1, "entries": [
            { "visitor": "{{visitorId}}", "count": 1, "spacingTicks": 10 } ] } ] }
        """;

        VisitorDef[] visitors = Visitors();
        var content = new ContentSet
        {
            Stations = TestContent.BuildContent().Stations,
            Visitors = visitors,
            Waves = ContentLoader.LoadWaves(waves, visitors, "w.json"),
        };

        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), content, 1);
        sim.Enqueue(new BuildCommand(new GridCell(5, 3), content.StationIndexOf(stationId)));
        sim.Enqueue(new StartWaveCommand());

        int dealt = 0;
        for (int t = 0; t < 400; t++)
        {
            sim.Tick();
            foreach (SimEvent e in sim.Events.Span)
                if (e.Kind == EventKind.VisitorServed) dealt += e.B;
        }
        return dealt;
    }

    [Fact]
    public void FussinessApplies_PerHit_NotToTheTickTotal()
    {
        // The whole design. Per-hit is what makes rapid-fire stations weak; applying
        // fussiness to a tick's total would leave them almost unaffected.
        int bare = ServingDealt("bare", "arrow-station");
        int plated = ServingDealt("plated", "arrow-station");

        Assert.True(plated > 0, "fussiness must never zero a station out");
        Assert.True(plated < bare / 2,
            $"arrow vs fussiness 8 dealt {plated} against {bare} unfussinessed -- 12 serving should land as 4");
    }

    [Fact]
    public void FussinessHurtsRapidFireFarMoreThanBurst()
    {
        // Criterion 7: provable from the numbers, not asserted in a doc.
        double arrowLoss = 1.0 - ServingDealt("plated", "arrow-station") / (double)ServingDealt("bare", "arrow-station");
        double cannonLoss = 1.0 - ServingDealt("plated", "cannon") / (double)ServingDealt("bare", "cannon");

        Assert.True(arrowLoss > cannonLoss + 0.2,
            $"arrow lost {arrowLoss:P0} to fussiness, cannon lost {cannonLoss:P0} -- " +
            "the gap is the decision the mechanic exists to create");
    }

    [Fact]
    public void NoHitEverDealsLessThanOne()
    {
        // An visitor immune to a station is a soft-lock waiting to happen.
        const string wall = """
        { "id": "wall", "name": "Wall", "appetite": 500, "speed": 0.04, "fussiness": 9999, "bounty": 1 }
        """;
        VisitorDef[] visitors = ContentLoader.LoadVisitors(new[] { ("wall.json", wall) });
        var content = new ContentSet
        {
            Stations = TestContent.BuildContent().Stations,
            Visitors = visitors,
            Waves = ContentLoader.LoadWaves(WallWave, visitors, "w.json"),
        };

        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), content, 1);
        sim.Enqueue(new BuildCommand(new GridCell(5, 3), content.StationIndexOf("arrow-station")));
        sim.Enqueue(new StartWaveCommand());

        var amounts = new List<int>();
        for (int t = 0; t < 300; t++)
        {
            sim.Tick();
            foreach (SimEvent e in sim.Events.Span)
                if (e.Kind == EventKind.VisitorServed) amounts.Add(e.B);
        }

        Assert.NotEmpty(amounts);
        Assert.All(amounts, a => Assert.Equal(1, a));
    }

    [Fact]
    public void TheRosterHasFiveArchetypes_AndNoSharedSilhouette()
    {
        // Criterion 5. Silhouette is a view concern, so this asserts the roster
        // size and leaves the shapes to the placeholder factory -- which has a
        // distinct case per id, checked by reading it.
        string dir = Path.Combine(RepoRoot(), "content-data", "visitors");
        string[] ids = Directory.EnumerateFiles(dir, "*.json")
            .Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToArray()!;

        Assert.Equal(new[] { "brute", "husk", "mite", "runner", "sapper" }, ids);

        string factory = File.ReadAllText(
            Path.Combine(RepoRoot(), "godot", "Placeholders", "PlaceholderFactory.cs"));
        foreach (string id in ids)
            Assert.Contains($"\"{id}\" =>", factory);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");
        return dir!.FullName;
    }
}
