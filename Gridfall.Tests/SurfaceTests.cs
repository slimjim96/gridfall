using Gridfall.Core;
using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Rivers and bridges. A **view-only** layer, on the same bargain elevation
/// shipped under: the simulation never reads it, so a board with a river through
/// it plays exactly as the same board without one.
///
/// That bargain only holds because water can only be painted where the
/// pathfinder already refuses to go. These tests are that rule, and the last one
/// is the whole point — a hash that does not move.
/// </summary>
public class SurfaceTests
{
    /// <summary>The arena with a north-south wall, so there is scenery to flood.</summary>
    private const string RiverMap = """
    {
      "id": "river", "width": 12, "height": 9,
      "cells": [
        "############",
        "#bbbb#bbbbb#",
        "#bbbb#bbbbb#",
        "#bbbb#bbbbb#",
        "S..........G",
        "#bbbb#bbbbb#",
        "#bbbb#bbbbb#",
        "#bbbb#bbbbb#",
        "############"
      ],
      "spawns": [{ "x": 0, "y": 4 }],
      "startingGold": 500, "startingPatience": 20,
      "surfaces": [
        ".....~......",
        ".....~......",
        ".....~......",
        ".....~......",
        ".....=......",
        ".....~......",
        ".....~......",
        ".....~......",
        ".....~......"
      ]
    }
    """;

    private static string WithSurfaces(string surfaces) => $$"""
    {
      "id": "t", "width": 12, "height": 9,
      "cells": [
        "############",
        "#bbbb#bbbbb#",
        "#bbbb#bbbbb#",
        "#bbbb#bbbbb#",
        "S..........G",
        "#bbbb#bbbbb#",
        "#bbbb#bbbbb#",
        "#bbbb#bbbbb#",
        "############"
      ],
      "spawns": [{ "x": 0, "y": 4 }],
      "startingGold": 500, "startingPatience": 20,
      "surfaces": {{surfaces}}
    }
    """;

    [Fact]
    public void ABoardWithNoSurfaceLayerIsAllGround()
    {
        MapDef map = ContentLoader.LoadMap(TestContent.ArenaMap, "arena.json");
        Assert.Empty(map.Surfaces);
        Assert.Equal(CellSurface.Ground, map.SurfaceAt(0));
    }

    [Fact]
    public void WaterAndSpansLoadFromTheGlyphs()
    {
        MapDef map = ContentLoader.LoadMap(RiverMap, "river.json");

        Assert.Equal(CellSurface.Water, map.SurfaceAt(map.Index(5, 0)));
        Assert.Equal(CellSurface.Span, map.SurfaceAt(map.Index(5, 4)));
        Assert.Equal(CellSurface.Ground, map.SurfaceAt(map.Index(0, 0)));
    }

    [Fact]
    public void WaterOnAWalkableCellIsRefused()
    {
        // The rule the whole view-only bargain rests on. A board that painted
        // water across the lane would LOOK like it had a river and PLAY like it
        // did not, and nothing downstream could tell the difference.
        var ex = Assert.Throws<ContentException>(() => ContentLoader.LoadMap(
            WithSurfaces("""
            [ "............", "............", "............", "............",
              "....~.......", "............", "............", "............",
              "............" ]
            """), "bad.json"));

        Assert.Contains("water is only legal on a blocked cell", ex.Message);
    }

    [Fact]
    public void ASpanOnAnUnwalkableCellIsRefused()
    {
        var ex = Assert.Throws<ContentException>(() => ContentLoader.LoadMap(
            WithSurfaces("""
            [ "....=.......", "............", "............", "............",
              "............", "............", "............", "............",
              "............" ]
            """), "bad.json"));

        Assert.Contains("a span is only legal on a walkable cell", ex.Message);
    }

    [Fact]
    public void AnUnknownGlyphIsRefused()
    {
        var ex = Assert.Throws<ContentException>(() => ContentLoader.LoadMap(
            WithSurfaces("""
            [ "....x.......", "............", "............", "............",
              "............", "............", "............", "............",
              "............" ]
            """), "bad.json"));

        Assert.Contains("expected one of", ex.Message);
    }

    [Fact]
    public void ABridgeThreeCellsLongDoesNotWarnAboutItsOwnMiddle()
    {
        // The first version of this check asked whether each span cell touched
        // water and warned about the middle of every bridge worth having.
        MapDraft draft = MapDraft.From(ContentLoader.LoadMap(RiverMap, "river.json"));
        draft.PaintSurface(new GridCell(4, 4), CellSurface.Span);
        draft.PaintSurface(new GridCell(6, 4), CellSurface.Span);

        Assert.DoesNotContain(MapValidator.Validate(draft),
            f => f.Message.Contains("touch no water"));
    }

    [Fact]
    public void ABridgeTouchingNoWaterWarnsButDoesNotBlock()
    {
        MapDraft draft = MapDraft.From(ContentLoader.LoadMap(TestContent.ArenaMap, "arena.json"));
        draft.PaintSurface(new GridCell(6, 4), CellSurface.Span);

        var findings = MapValidator.Validate(draft);
        Assert.False(MapValidator.HasErrors(findings));
        Assert.Contains(findings, f => f.Message.Contains("touch no water"));
    }

    [Fact]
    public void SurfacesSurviveAnEditorRoundTrip()
    {
        // Heights were carried through From/ToMapDef/ToJson for exactly this
        // reason. An editor that drops the layer drains every river on the first
        // open-and-save, and nothing would say so.
        MapDef original = ContentLoader.LoadMap(RiverMap, "river.json");
        MapDef roundTripped = ContentLoader.LoadMap(
            MapDraft.From(original).ToJson(), "round-trip.json");

        Assert.Equal(original.Surfaces, roundTripped.Surfaces);
    }

    [Fact]
    public void ADryBoardWritesNoSurfaceLayerAtAll()
    {
        // "All ground" has to stay the ABSENCE of the field. A layer of dots
        // would be a diff on every map that has no river in it.
        MapDraft draft = MapDraft.From(ContentLoader.LoadMap(TestContent.ArenaMap, "arena.json"));
        Assert.DoesNotContain("surfaces", draft.ToJson());
    }

    [Fact]
    public void ARiverChangesNothingTheSimulationCanSee()
    {
        // The claim, as a hash. Same board, same seed, same commands, one with a
        // river painted across its scenery and one without: byte-identical.
        static ulong Run(string mapJson)
        {
            MapDef map = ContentLoader.LoadMap(mapJson, "m.json");
            ContentSet content = TestContent.BuildContent();
            var sim = new Sim(map, content, 1);

            sim.Enqueue(new BuildCommand(new GridCell(2, 3), content.StationIndexOf("arrow-station")));
            sim.Enqueue(new BuildCommand(new GridCell(7, 5), content.StationIndexOf("cannon")));
            sim.Enqueue(new StartWaveCommand());
            for (int t = 0; t < 900; t++) sim.Tick();
            return sim.Hash();
        }

        string dry = WithSurfaces("""
        [ "............", "............", "............", "............",
          "............", "............", "............", "............",
          "............" ]
        """);

        Assert.Equal(Run(dry), Run(RiverMap.Replace("\"id\": \"river\"", "\"id\": \"t\"")));
    }
}
