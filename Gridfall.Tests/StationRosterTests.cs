using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// A board may offer only some of the stations. The toolbar draws that roster, and
/// these tests are what keep the toolbar and the simulation agreeing about it --
/// a slot on screen and a build the sim accepts have to be the same set, or the
/// player meets a refusal they can do nothing about.
///
/// The rule patience on <see cref="MapDef.Offers"/> so that both callers read one
/// definition: `CommandSystem` enforces it and the view's StationBar draws it.
/// </summary>
public class StationRosterTests
{
    /// <summary>The arena, restricted to whatever ids are passed.</summary>
    private static MapDef Restricted(params string[] stationIds)
    {
        MapDraft draft = MapDraft.From(TestContent.Map(TestContent.ArenaMap));
        draft.StationIds.AddRange(stationIds);
        return draft.ToMapDef();
    }


    [Fact]
    public void AnEmptyRosterOffersEveryStation()
    {
        // The back-compatible default, and the reason every existing map keeps
        // working untouched. Empty is "all", not "none".
        MapDef map = TestContent.Map(TestContent.ArenaMap);
        var sim = new Sim(map, TestContent.BuildContent(), 1);

        Assert.Empty(map.StationIds);
        for (ushort i = 0; i < sim.Content.Stations.Length; i++)
            Assert.True(map.Offers(sim.Content, i));
    }

    [Fact]
    public void ARosterOffersOnlyWhatItLists()
    {
        var sim = new Sim(Restricted("arrow-station"), TestContent.BuildContent(), 1);

        Assert.True(sim.Map.Offers(sim.Content, sim.Content.StationIndexOf("arrow-station")));
        Assert.False(sim.Map.Offers(sim.Content, sim.Content.StationIndexOf("cannon")));
    }

    [Fact]
    public void BuildingAStationTheBoardDoesNotOfferIsRefused()
    {
        var sim = new Sim(Restricted("arrow-station"), TestContent.BuildContent(), 1);
        sim.Enqueue(new BuildCommand(new GridCell(3, 2), sim.Content.StationIndexOf("cannon")));
        sim.Tick();

        Assert.Equal(0, sim.State.StationCount);
        Assert.Contains(sim.Events.Span.ToArray(), e =>
            e.Kind == EventKind.BuildRejected && e.A == (int)RejectReason.StationNotOnThisBoard);
    }

    [Fact]
    public void BuildingAnOfferedStationStillWorks()
    {
        // The other half of the previous test: the roster must refuse the one
        // station, not every station. A check inverted by accident would pass a test
        // that only asserted the refusal.
        var sim = new Sim(Restricted("arrow-station"), TestContent.BuildContent(), 1);
        sim.Enqueue(new BuildCommand(new GridCell(3, 2), sim.Content.StationIndexOf("arrow-station")));
        sim.Tick();

        Assert.Equal(1, sim.State.StationCount);
    }

    [Fact]
    public void ARosterSurvivesTheEditorRoundTrip()
    {
        // The editor loads a map into a draft and writes it back. A roster lost
        // here would silently re-offer every station on any board somebody opened
        // and saved -- and the board would still validate, so nothing would say so.
        MapDef before = Restricted("cannon");
        MapDef after = ContentLoader.LoadMap(MapDraft.From(before).ToJson(), "roundtrip.json");

        Assert.Equal(new[] { "cannon" }, after.StationIds);
    }

    [Fact]
    public void AMapWithNoRosterWritesNoStationsField()
    {
        // "Absent" and "every station listed" differ the moment a third station is
        // added, so the serialiser must not turn one into the other.
        string json = MapDraft.From(TestContent.Map(TestContent.ArenaMap)).ToJson();
        Assert.DoesNotContain("\"stations\"", json);
    }

    [Fact]
    public void AnEmptyStationsArrayIsRejected()
    {
        // A board that offers nothing is a typo every time, and it would present
        // as an empty toolbar with no explanation.
        string json = TestContent.ArenaMap.TrimEnd().TrimEnd('}') + ", \"stations\": [] }";
        ContentException ex = Assert.Throws<ContentException>(
            () => ContentLoader.LoadMap(json, "empty.json"));
        Assert.Contains("stations", ex.Message);
    }

    [Fact]
    public void ARosterNamingAnUnknownStationFailsLoudly()
    {
        // The map and the station files load independently, so Sim's constructor is
        // the first moment both are in hand. Unchecked, a typo shows up as a
        // station quietly missing from one board's toolbar.
        ContentException ex = Assert.Throws<ContentException>(
            () => new Sim(Restricted("trebuchet"), TestContent.BuildContent(), 1));

        Assert.Contains("trebuchet", ex.Message);
    }

    [Fact]
    public void EveryShippedMapRosterNamesRealStations()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");

        string mapDir = Path.Combine(dir!.FullName, "content-data", "maps");
        ContentSet content = TestContent.BuildContent();

        foreach (string file in Directory.GetFiles(mapDir, "*.json"))
        {
            string name = Path.GetFileName(file);
            MapDef map = ContentLoader.LoadMap(File.ReadAllText(file), name);
            foreach (string stationId in map.StationIds)
                Assert.True(content.Stations.Any(t => t.Id == stationId),
                    $"{name} offers '{stationId}', which is not a station");
        }
    }
}
