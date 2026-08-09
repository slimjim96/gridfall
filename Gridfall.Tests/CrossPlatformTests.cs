using System.Globalization;
using Gridfall.Core;
using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The simulation must produce identical state on Linux, Windows and macOS, and
/// content written on one must be byte-identical to content written on another.
///
/// Determinism is the project's first rule, and "deterministic" quietly means
/// "on this machine" until something proves otherwise. These are the three ways
/// a .NET sim usually stops being portable — none of them fail loudly, and all
/// three would show up as a mysteriously diverging trace weeks later.
/// </summary>
public class CrossPlatformTests
{
    /// <summary>
    /// A locale where the decimal separator is a comma.
    ///
    /// The classic .NET portability bug: `double.Parse("0.06")` returns 6 under
    /// de-DE, so a station's range or a visitor's speed silently becomes a
    /// hundred times too big on a German-locale machine. `ContentLoader.ParseFix`
    /// avoids it by reading the raw JSON text digit by digit rather than going
    /// through a culture-aware parse — this test is what keeps it that way.
    /// </summary>
    private static readonly CultureInfo CommaDecimal = new("de-DE");

    private static ulong HashAfter(int ticks)
    {
        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), TestContent.BuildContent(), 1);
        sim.Enqueue(new BuildCommand(new GridCell(3, 2), sim.Content.StationIndexOf("arrow-station")));
        sim.Enqueue(new StartWaveCommand());
        for (int t = 0; t < ticks; t++) sim.Tick();
        return sim.Hash();
    }

    [Fact]
    public void TheCommaDecimalLocaleIsActuallyInEffect()
    {
        // Guards the guard. Under InvariantGlobalization the de-DE lookup falls
        // back to invariant and every locale test below passes for the wrong
        // reason -- silently, which is the failure mode they exist to catch.
        Assert.Equal(",", CommaDecimal.NumberFormat.NumberDecimalSeparator);
        // The actual hazard: under de-DE "." is a GROUP separator, so a speed of
        // 0.06 parses as 6 -- a hundred times too fast, no exception, no warning.
        Assert.Equal(6d, double.Parse("0.06", CommaDecimal));
    }

    [Fact]
    public void TheSimHashesIdenticallyUnderACommaDecimalLocale()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        ulong invariant = HashAfter(200);
        try
        {
            CultureInfo.CurrentCulture = CommaDecimal;
            CultureInfo.CurrentUICulture = CommaDecimal;
            Assert.Equal(invariant, HashAfter(200));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact]
    public void ContentParsesIdenticallyUnderACommaDecimalLocale()
    {
        // The narrower version of the test above: if a decimal ever went through
        // a culture-aware parse, speed and range are where it would show first.
        CultureInfo before = CultureInfo.CurrentCulture;
        ContentSet invariant = TestContent.BuildContent();
        try
        {
            CultureInfo.CurrentCulture = CommaDecimal;
            ContentSet german = TestContent.BuildContent();

            for (ushort i = 0; i < invariant.Stations.Length; i++)
                Assert.Equal(invariant.Station(i).Range.Raw, german.Station(i).Range.Raw);
            for (ushort i = 0; i < invariant.Visitors.Length; i++)
                Assert.Equal(invariant.Visitors[i].Speed.Raw, german.Visitors[i].Speed.Raw);
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void SavedMapsUseUnixNewlinesOnEveryPlatform()
    {
        // StringBuilder.AppendLine emits Environment.NewLine, so the board editor
        // used to write CRLF on Windows and LF everywhere else. The map still
        // loads either way -- but it shows as a whole-file diff, and the map
        // generator's idempotence is proven by byte comparison.
        string json = MapDraft.From(TestContent.Map(TestContent.ArenaMap)).ToJson();

        Assert.DoesNotContain("\r", json);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void ShippedContentHasNoCarriageReturns()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");

        string data = Path.Combine(dir!.FullName, "content-data");
        foreach (string f in Directory.GetFiles(data, "*.json", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            // A CRLF here means someone's git checkout normalised content files,
            // which .gitattributes exists to prevent. Loading still works; byte
            // comparison between two machines stops working.
            Assert.DoesNotContain("\r", File.ReadAllText(f));
        }
    }
}
