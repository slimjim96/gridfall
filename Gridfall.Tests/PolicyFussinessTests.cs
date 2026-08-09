using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Io;
using Gridfall.Verify;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The balance harness's shopping decision.
///
/// <see cref="FussinessTests"/> proves the mechanic works in the simulation.
/// These prove the harness can SEE it -- which for the whole history of this
/// repo it could not, because <c>PlayPolicy</c> ranked stations on base serving
/// and therefore bought the arrow station on every board, in every wave, in
/// every run ever measured.
///
/// Two claims live here, and they point opposite ways:
///   1. The policy flips to burst when the census warrants it. (It does.)
///   2. No shipped wave table ever warrants it. (None does, by a factor of ~3.)
///
/// Claim 2 failing is good news, not a regression -- see its message.
/// </summary>
public class PolicyFussinessTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");
        return dir!.FullName;
    }

    /// <summary>
    /// The SHIPPED stations and visitors, with a wave table written for the test.
    /// Shipped defs on purpose: the crossover is a claim about the real roster,
    /// and a fixture roster would let it pass while the real one drifted.
    /// </summary>
    private static ContentSet ContentWithWaves(string wavesJson)
    {
        string data = Path.Combine(RepoRoot(), "content-data");
        StationDef[] stations = ContentLoader.LoadStations(ReadDir(Path.Combine(data, "stations")));
        VisitorDef[] visitors = ContentLoader.LoadVisitors(ReadDir(Path.Combine(data, "visitors")));
        return new ContentSet
        {
            Stations = stations,
            Visitors = visitors,
            Waves = ContentLoader.LoadWaves(wavesJson, visitors, "test-waves.json"),
        };
    }

    private static (string, string)[] ReadDir(string dir)
        => Directory.EnumerateFiles(dir, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)))
            .ToArray();

    /// <summary>A one-wave table of runners and husks in the given counts.</summary>
    private static string MixedWave(int runners, int husks) => $$"""
    { "map": "t", "waves": [ { "index": 1, "entries": [
        { "visitor": "runner", "count": {{runners}}, "spacingTicks": 10 },
        { "visitor": "husk",   "count": {{husks}},   "spacingTicks": 10 } ] } ] }
    """;

    private static string BestBuy(ContentSet content, int wavesStarted)
    {
        var census = new VisitorCensus(content);
        census.ObserveWavesStarted(wavesStarted);

        StationDef best = content.Stations[0];
        foreach (StationDef def in content.Stations)
            if (census.ValuePerGold(def) > census.ValuePerGold(best)) best = def;
        return best.Id;
    }

    [Fact]
    public void AnUnfussyWave_BuysTheCheapFastStation()
        => Assert.Equal("arrow-station", BestBuy(ContentWithWaves(MixedWave(runners: 10, husks: 0)), 1));

    [Fact]
    public void AFussyWave_BuysBurstInstead()
    {
        // 10 husks (1200 appetite) against 2 runners (120) -- 91% of the work is
        // armoured, so the arrow station's 12 lands as 4 and the cannon wins.
        // This is the decision the roster exists to create, and until now nothing
        // in the repo had ever made it.
        Assert.Equal("cannon", BestBuy(ContentWithWaves(MixedWave(runners: 2, husks: 10)), 1));
    }

    [Fact]
    public void TheCrossoverIsAtHalfTheWave_ByAppetite()
    {
        // Arithmetic, so it is worth stating exactly: arrow lands 12-8=4 on a husk
        // and 12 on a runner; cannon lands 32 and 40. Per gold per tick the two
        // are equal when the mix averages fussiness 4 -- which, with only husk
        // (8) and runner (0) in it, is exactly half the wave BY APPETITE.
        //
        // 120 appetite per husk against 60 per runner, so half the appetite is a
        // third of the head count. That factor of two is why "one in five visitors
        // is a husk" sounds like plenty and is not close.
        ContentSet under = ContentWithWaves(MixedWave(runners: 12, husks: 5));   // 720 vs 600 = 45%
        ContentSet over = ContentWithWaves(MixedWave(runners: 8, husks: 5));     // 480 vs 600 = 56%

        Assert.Equal("arrow-station", BestBuy(under, 1));
        Assert.Equal("cannon", BestBuy(over, 1));
    }

    [Fact]
    public void ThePolicyKnowsOnlyWavesThatHaveStarted()
    {
        // The honesty boundary. The game shows no wave preview -- the HUD prints
        // "wave N incoming" and nothing about its composition -- so a policy that
        // weighted against the wave it is ABOUT to fight would be reading the wave
        // table, which no player can do. Before wave 1 the census is empty and the
        // ranking is the unreduced one, even though the table is nothing but husks.
        ContentSet content = ContentWithWaves(MixedWave(runners: 0, husks: 20));

        Assert.Equal("arrow-station", BestBuy(content, wavesStarted: 0));
        Assert.Equal("cannon", BestBuy(content, wavesStarted: 1));
    }

    [Fact]
    public void ThePolicyActuallyBuysTheCannon_WhenTheCensusWarrantsIt()
    {
        // End to end through the real Sim: the ranking above is only worth
        // anything if it reaches a BuildCommand.
        string wave = """{ "index": %I%, "entries": [ { "visitor": "husk", "count": 12, "spacingTicks": 20 } ] }""";
        ContentSet content = ContentWithWaves(
            "{ \"map\": \"t\", \"waves\": ["
            + string.Join(",", Enumerable.Range(1, 6).Select(i => wave.Replace("%I%", i.ToString())))
            + "] }");

        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), content, 1);
        var policy = new PlayPolicy(sim, 1);
        ushort arrowIdx = content.StationIndexOf("arrow-station");
        ushort cannonIdx = content.StationIndexOf("cannon");

        // Split at the first wave start. Everything before it is bought blind --
        // the census is empty and the policy is right to buy the cheap station,
        // because it has met nothing. Only the buys AFTER that are a decision.
        int arrowBlind = 0, cannonBlind = 0;
        for (int t = 0; t < 8000; t++)
        {
            if (sim.State.WaveIndex == 1 && arrowBlind + cannonBlind == 0)
            {
                arrowBlind = policy.BoughtOf(arrowIdx);
                cannonBlind = policy.BoughtOf(cannonIdx);
            }
            policy.Update();
            sim.Tick();
        }

        int arrow = policy.BoughtOf(arrowIdx) - arrowBlind;
        int cannon = policy.BoughtOf(cannonIdx) - cannonBlind;

        Assert.True(cannon > 0,
            $"after meeting husks the policy built {arrow} arrow stations and {cannon} cannons");
        Assert.True(cannon > arrow,
            $"burst should be the majority buy here: {cannon} cannons against {arrow} arrow stations");
    }

    [Fact]
    public void NoShippedWaveTableEverReachesTheCrossover()
    {
        // The finding, as a test. Every shipped table peaks around fussiness 1.5
        // averaged by appetite, against a crossover at 4 -- so teaching the policy
        // about fussiness changed no shipped balance figure at all. The husk is
        // present in the content and inert in the decision.
        //
        // IF THIS FAILS, the content finally asks the question: some wave now
        // makes burst the correct buy, the balance figures for that map have
        // moved, and 2026-08-09-policy-fussiness-balance.md needs re-running
        // rather than the test needs relaxing.
        string data = Path.Combine(RepoRoot(), "content-data");
        StationDef[] stations = ContentLoader.LoadStations(ReadDir(Path.Combine(data, "stations")));
        VisitorDef[] visitors = ContentLoader.LoadVisitors(ReadDir(Path.Combine(data, "visitors")));

        var offenders = new List<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(data, "waves"), "*.json")
                                         .OrderBy(f => f, StringComparer.Ordinal))
        {
            WaveDef[] waves = ContentLoader.LoadWaves(File.ReadAllText(file), visitors, file);
            var content = new ContentSet { Stations = stations, Visitors = visitors, Waves = waves };

            // Per single wave, which is the most generous reading available: the
            // policy's own census is cumulative and therefore even flatter.
            for (int w = 0; w < waves.Length; w++)
            {
                VisitorCensus census = VisitorCensus.ForWave(content, w);
                StationDef arrow = stations.Single(s => s.Id == "arrow-station");
                StationDef cannon = stations.Single(s => s.Id == "cannon");
                if (census.ValuePerGold(cannon) >= census.ValuePerGold(arrow))
                    offenders.Add($"{Path.GetFileNameWithoutExtension(file)} wave {w + 1}");
            }
        }

        Assert.True(offenders.Count == 0,
            "a shipped wave now makes burst the correct buy -- good, and the balance "
            + "reports that assume it never does are stale: " + string.Join(", ", offenders));
    }
}
