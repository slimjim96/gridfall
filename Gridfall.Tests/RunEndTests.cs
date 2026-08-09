using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// A run has to be able to end. Before this, GameOver fired into nothing and
/// clearing the last wave produced no signal at all — so the game could neither
/// be lost nor won, and kept running at zero patience indefinitely.
/// </summary>
public class RunEndTests
{
    [Fact]
    public void ClearingTheLastWaveAliveEmitsRunComplete()
    {
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), TestContent.BuildContent(), 1);

        bool sawRunComplete = false;
        for (int wave = 0; wave < 40 && !sawRunComplete; wave++)
        {
            sim.Enqueue(new StartWaveCommand());
            for (int t = 0; t < 4000 && !sawRunComplete; t++)
            {
                sim.Events.Clear();
                sim.Tick();
                foreach (SimEvent e in sim.Events.Span)
                    if (e.Kind == EventKind.RunComplete) sawRunComplete = true;
            }
        }

        Assert.True(sawRunComplete, "clearing every wave alive produced no RunComplete");
    }

    [Fact]
    public void RunCompleteFiresExactlyOnce()
    {
        // It rides the final WaveCleared transition rather than a stored flag,
        // which is what keeps it out of the hash -- but a transition that can
        // repeat would spam the view and re-show the end screen forever.
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), TestContent.BuildContent(), 1);

        int count = 0;
        for (int wave = 0; wave < 40; wave++)
        {
            sim.Enqueue(new StartWaveCommand());
            for (int t = 0; t < 4000; t++)
            {
                sim.Events.Clear();
                sim.Tick();
                foreach (SimEvent e in sim.Events.Span)
                    if (e.Kind == EventKind.RunComplete) count++;
            }
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public void RunWaves_TruncatesTheTable()
    {
        VisitorDef[] visitors = ShippedVisitors();
        string json = File.ReadAllText(Path.Combine(RepoRoot(), "content-data", "waves", "crossroads.json"));

        int full = ContentLoader.LoadWaves(json, visitors, "crossroads.json").Length;

        string truncated = json.TrimEnd().TrimEnd('}') + ", \"runWaves\": 5 }";
        Assert.Equal(5, ContentLoader.LoadWaves(truncated, visitors, "t.json").Length);
        Assert.True(full > 5, "fixture should have more waves than the truncation");
    }

    [Fact]
    public void RunWaves_OutsideTheAuthoredTableIsRefused()
    {
        // Silently clamping would let a typo shorten a run and look intentional.
        VisitorDef[] visitors = ShippedVisitors();
        string json = File.ReadAllText(Path.Combine(RepoRoot(), "content-data", "waves", "crossroads.json"));

        foreach (int bad in new[] { 0, 99 })
        {
            string broken = json.TrimEnd().TrimEnd('}') + $", \"runWaves\": {bad} }}";
            Assert.Throws<ContentException>(() => ContentLoader.LoadWaves(broken, visitors, "t.json"));
        }
    }

    /// <summary>
    /// The real roster, not TestContent's — the shipped wave tables name visitors
    /// (mite, sapper) that the test fixture does not carry.
    /// </summary>
    private static VisitorDef[] ShippedVisitors()
    {
        string dir = Path.Combine(RepoRoot(), "content-data", "visitors");
        return ContentLoader.LoadVisitors(
            Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => (Path.GetFileName(f), File.ReadAllText(f))));
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
