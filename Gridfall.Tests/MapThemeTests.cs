using System.Text.RegularExpressions;
using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// A map names its terrain palette; the view owns the palettes.
///
/// Core deliberately holds no list of valid themes -- it carries the string and
/// never reads it, so nothing below the boundary knows a colour exists. The cost
/// of that is a typo reaching the renderer, where it falls back to the default.
/// These tests are what stop the fallback from hiding one.
/// </summary>
public class MapThemeTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");
        return dir!;
    }

    /// <summary>
    /// The registered theme ids, read out of the view's source.
    ///
    /// Reading source rather than duplicating the list here follows
    /// SourcePurityTests: the test project cannot reference the Godot assembly,
    /// and a second copy of the list would be a second thing to forget.
    /// </summary>
    private static HashSet<string> RegisteredThemes()
    {
        string path = Path.Combine(RepoRoot().FullName, "godot", "View", "TerrainTheme.cs");
        Assert.True(File.Exists(path), $"theme registry not found at {path}");

        var ids = new HashSet<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(path), @"\[""(?<id>[a-z-]+)""\]\s*=\s*new TerrainTheme"))
            ids.Add(m.Groups["id"].Value);

        Assert.True(ids.Count >= 2, "parsed suspiciously few themes -- has the registry's shape changed?");

        // A theme may exist as a folder of tiles with no colour ramp in code at
        // all -- that is the point of presentation/tiles/, and TerrainTheme.IsKnown
        // accepts both. A test that only read the C# would reject a map naming a
        // perfectly valid tile theme.
        string tiles = Path.Combine(RepoRoot().FullName, "presentation", "tiles");
        if (Directory.Exists(tiles))
            foreach (string dir in Directory.GetDirectories(tiles))
                // A folder with no PNGs is NOT a theme: TileLibrary drops it, so
                // a map naming it would fall back to slate. Counting bare
                // directories here would have let that pass the test and
                // surprise somebody at runtime.
                if (Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories).Length > 0)
                    ids.Add(Path.GetFileName(dir));

        return ids;
    }

    [Fact]
    public void EveryShippedMapNamesAKnownTheme()
    {
        HashSet<string> known = RegisteredThemes();
        string mapDir = Path.Combine(RepoRoot().FullName, "content-data", "maps");

        foreach (string file in Directory.GetFiles(mapDir, "*.json"))
        {
            MapDef map = ContentLoader.LoadMap(File.ReadAllText(file), Path.GetFileName(file));
            Assert.True(known.Contains(map.Theme),
                $"{Path.GetFileName(file)} names theme '{map.Theme}', which is not registered. " +
                $"Known: {string.Join(", ", known.OrderBy(t => t))}");
        }
    }

    [Fact]
    public void EveryShippedMapAuthorsItsThemeExplicitly()
    {
        // Relying on the default means the map's look is nobody's decision. Same
        // reasoning as repairPercent being authored on every tower def.
        string mapDir = Path.Combine(RepoRoot().FullName, "content-data", "maps");
        foreach (string file in Directory.GetFiles(mapDir, "*.json"))
            Assert.Contains("\"theme\"", File.ReadAllText(file));
    }

    [Fact]
    public void AMapWithNoThemeLoadsAsTheDefault()
    {
        // Back-compat: a hand-written map from before themes existed must still
        // open, and must look exactly as it did.
        const string json = """
        { "id": "t", "width": 12, "height": 8,
          "cells": [
            "############",
            "#..........#",
            "#..........#",
            "S..........G",
            "#..........#",
            "#..........#",
            "#..........#",
            "############" ],
          "spawns": [{ "x": 0, "y": 3 }],
          "startingGold": 500, "startingLives": 20 }
        """;
        Assert.Equal("slate", ContentLoader.LoadMap(json, "t.json").Theme);
    }

    [Fact]
    public void TheThemeSurvivesAnEditorRoundTrip()
    {
        // The editor writes the format the loader reads. A theme that survives
        // load but not save would silently reset every map someone opened.
        MapDef before = ContentLoader.LoadMap(
            File.ReadAllText(Path.Combine(RepoRoot().FullName, "content-data", "maps", "gauntlet.json")),
            "gauntlet.json");

        MapDraft draft = MapDraft.From(before);
        MapDef after = ContentLoader.LoadMap(draft.ToJson(), "roundtrip.json");

        Assert.Equal(before.Theme, after.Theme);
        Assert.Equal("mountain", after.Theme);
    }

    [Fact]
    public void TheThemeIsNotSimulationState()
    {
        // Two maps identical but for their theme must hash identically at tick 0.
        // If this ever fails, a presentational field has leaked into the sim and
        // every recorded trace is now theme-dependent.
        MapDraft draft = MapDraft.From(ContentLoader.LoadMap(
            File.ReadAllText(Path.Combine(RepoRoot().FullName, "content-data", "maps", "crossroads.json")),
            "crossroads.json"));

        draft.Theme = "slate";
        var a = new Gridfall.Core.Sim(draft.ToMapDef(), TestContent.BuildContent(), 1);
        draft.Theme = "space";
        var b = new Gridfall.Core.Sim(draft.ToMapDef(), TestContent.BuildContent(), 1);

        for (int t = 0; t < 30; t++) { a.Tick(); b.Tick(); }
        Assert.Equal(a.Hash(), b.Hash());
    }
}
