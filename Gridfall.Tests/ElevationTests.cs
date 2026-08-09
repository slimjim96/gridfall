using Gridfall.Core;
using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Elevation is presentation, not simulation (docs/iso-grid.md §Elevation).
///
/// The point of these tests is the negative one: a board with hills must play
/// **identically** to the same board flat. If that ever stops being true,
/// elevation has quietly become simulation input and every committed trace is
/// suspect — so the first test here is the one that matters most.
/// </summary>
public class ElevationTests
{
    private const string Flat = TestContent.ArenaMap;

    /// <summary>The arena with a height field applied.</summary>
    private static MapDef Hilly()
    {
        MapDraft draft = MapDraft.From(TestContent.Map(Flat));
        var levels = new byte[draft.Width * draft.Height];
        for (int y = 0; y < draft.Height; y++)
            for (int x = 0; x < draft.Width; x++)
                levels[y * draft.Width + x] = (byte)((x + y) % 5);
        draft.Heights = levels;
        return draft.ToMapDef();
    }

    private static ulong HashAfter(MapDef map, int ticks)
    {
        var sim = new Sim(map, TestContent.BuildContent(), 1);
        sim.Enqueue(new BuildCommand(new GridCell(3, 2), sim.Content.StationIndexOf("arrow-station")));
        sim.Enqueue(new StartWaveCommand());
        for (int t = 0; t < ticks; t++) sim.Tick();
        return sim.Hash();
    }

    [Fact]
    public void AHillyBoardHashesExactlyLikeTheSameBoardFlat()
    {
        // The whole claim in one assertion. Elevation may not touch movement,
        // pathing, range or the state hash.
        Assert.Equal(HashAfter(TestContent.Map(Flat), 300), HashAfter(Hilly(), 300));
    }

    [Fact]
    public void AFlatBoardCarriesNoHeightsAtAll()
    {
        // Absent, not a field of zeroes: a map written before elevation existed
        // must round-trip unchanged, and HeightAt has to answer 0 either way.
        MapDef map = TestContent.Map(Flat);
        Assert.Empty(map.Heights);
        Assert.Equal(0, map.HeightAt(0));
    }

    [Fact]
    public void HeightsSurviveTheEditorRoundTrip()
    {
        // The editor loads into a draft and writes back. Heights lost here would
        // silently flatten any board somebody opened and saved, and the map would
        // still validate.
        MapDef before = Hilly();
        MapDef after = ContentLoader.LoadMap(MapDraft.From(before).ToJson(), "roundtrip.json");

        Assert.Equal(before.Heights, after.Heights);
    }

    [Fact]
    public void AFlatBoardWritesNoHeightsField()
    {
        string json = MapDraft.From(TestContent.Map(Flat)).ToJson();
        Assert.DoesNotContain("\"heights\"", json);
    }

    [Theory]
    [InlineData("\"heights\": [\"000\"]", "row")]                 // wrong width
    [InlineData("\"heights\": \"0000\"", "array")]                // not an array
    public void AMalformedHeightFieldIsRejected(string field, string expected)
    {
        // Loudly, at load. A height field that is quietly ignored is a board that
        // renders flat for no stated reason.
        string json = Flat.TrimEnd().TrimEnd('}') + ", " + field + " }";
        ContentException ex = Assert.Throws<ContentException>(() => ContentLoader.LoadMap(json, "bad.json"));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void ANonDigitHeightIsRejected()
    {
        MapDraft draft = MapDraft.From(TestContent.Map(Flat));
        string rows = string.Join(", ",
            Enumerable.Repeat("\"" + new string('0', draft.Width) + "\"", draft.Height - 1)
                      .Append("\"" + new string('x', draft.Width) + "\""));
        string json = Flat.TrimEnd().TrimEnd('}') + ", \"heights\": [" + rows + "] }";

        ContentException ex = Assert.Throws<ContentException>(() => ContentLoader.LoadMap(json, "bad.json"));
        Assert.Contains("digit", ex.Message);
    }

    [Fact]
    public void SculptingAllocatesTheFieldOnlyWhenUsed()
    {
        // Flat has to stay the ABSENCE of the field, not a field of zeroes --
        // otherwise every map the editor touches starts writing a heights block
        // that says nothing.
        MapDraft draft = MapDraft.From(TestContent.Map(Flat));
        Assert.Empty(draft.Heights);

        draft.Raise(new GridCell(2, 2), +2);
        Assert.Equal(2, draft.HeightAt(new GridCell(2, 2)));
        Assert.Equal(0, draft.HeightAt(new GridCell(3, 2)));

        // Back to flat: the field exists but is all zeroes, and the serialiser
        // must still omit it.
        draft.Raise(new GridCell(2, 2), -2);
        Assert.DoesNotContain("\"heights\"", draft.ToJson());
    }

    [Fact]
    public void SculptingClampsRatherThanWrapping()
    {
        MapDraft draft = MapDraft.From(TestContent.Map(Flat));
        var cell = new GridCell(2, 2);

        draft.Raise(cell, -5);
        Assert.Equal(0, draft.HeightAt(cell));      // a byte wrapping here would be 251

        draft.Raise(cell, +40);
        Assert.Equal(9, draft.HeightAt(cell));
    }

    [Fact]
    public void SculptingSurvivesAnUndoSnapshot()
    {
        // The editor's undo is Clone(), which round-trips through ToMapDef.
        MapDraft draft = MapDraft.From(TestContent.Map(Flat));
        draft.Raise(new GridCell(4, 3), +3);

        Assert.Equal(3, draft.Clone().HeightAt(new GridCell(4, 3)));
    }

    [Fact]
    public void EveryShippedHeightFieldMatchesItsMap()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");

        string mapDir = Path.Combine(dir!.FullName, "content-data", "maps");
        foreach (string file in Directory.GetFiles(mapDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(file);
            MapDef map = ContentLoader.LoadMap(File.ReadAllText(file), name);
            if (map.Heights.Length == 0) continue;

            Assert.True(map.Heights.Length == map.Width * map.Height,
                $"{name}: height field is {map.Heights.Length}, expected {map.Width * map.Height}");
        }
    }
}
