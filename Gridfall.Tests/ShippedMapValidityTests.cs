using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Every map in content-data/maps, put through the game's own validator.
///
/// This exists because nothing did it. `spiral`, `stepwell` and `driftway`
/// shipped with 5, 6 and 2 walled-off buildable cells while every document in
/// the repo said all ten maps passed. Three things had to line up: the generator
/// re-implemented MapValidator and its copy omitted the check, `Verify -- maps`
/// reported geometry and never called the validator at all, and the only tool
/// that *did* call it -- the board editor -- could not be run because its capture
/// path painted over the map first.
///
/// The lesson is not "add a check to the generator". It is that a rule with one
/// authority and three paraphrases has no authority. These tests call the real
/// validator on the real files, in CI, where none of the three can route around
/// it.
/// </summary>
public class ShippedMapValidityTests
{
    private static string MapDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");
        return Path.Combine(dir!.FullName, "content-data", "maps");
    }

    private static IEnumerable<(string Name, MapDraft Draft)> ShippedMaps()
    {
        string[] files = Directory.GetFiles(MapDir(), "*.json");
        Assert.True(files.Length >= 10, $"expected the shipped level set, found {files.Length} maps");

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            yield return (name, MapDraft.From(ContentLoader.LoadMap(File.ReadAllText(file), name)));
        }
    }

    [Fact]
    public void NoShippedMapHasAValidationError()
    {
        foreach ((string name, MapDraft draft) in ShippedMaps())
        {
            List<MapFinding> findings = MapValidator.Validate(draft);
            IEnumerable<string> errors = findings
                .Where(f => f.Severity == MapSeverity.Error)
                .Select(f => f.ToString());

            Assert.True(!errors.Any(), $"{name}: {string.Join("; ", errors)}");
        }
    }

    [Fact]
    public void NoShippedMapHasWalledOffBuildableCells()
    {
        // The specific regression. A buildable cell the creeps can never reach is
        // not a blemish, it is a decoy: the player buys a tower there and the gold
        // is worse spent than it would have been. (The tower does fire --
        // TargetingSystem acquires on range alone -- it is simply bad value, and
        // the policy keeps no reserve.) Sealing spiral's five moved it from 41.3%
        // to 25.3% of runs lost over 150 runs, from outside the difficulty band
        // into it, on five cells. See content-data/docs/example-levels.md.
        foreach ((string name, MapDraft draft) in ShippedMaps())
        {
            string? stranded = MapValidator.Validate(draft)
                .Where(f => f.Severity == MapSeverity.Warning)
                .Select(f => f.Message)
                .FirstOrDefault(m => m.Contains("walled off"));

            Assert.True(stranded is null, $"{name}: {stranded}");
        }
    }

    [Fact]
    public void TheValidatorStillCatchesAWalledOffCell()
    {
        // Guards the guard. If the check above ever passes because the rule was
        // deleted rather than because the maps are clean, this fails.
        //
        // The pocket is the 'b' at (1,1): ringed by wall on every side, so the
        // route from S to G cannot reach it.
        const string json = """
        { "id": "pocket", "width": 12, "height": 8,
          "cells": [
            "############",
            "#b#########.",
            "###########.",
            "S..........G",
            "#..........#",
            "#..........#",
            "#..........#",
            "############" ],
          "spawns": [{ "x": 0, "y": 3 }],
          "startingGold": 500, "startingLives": 20 }
        """;

        MapDraft draft = MapDraft.From(ContentLoader.LoadMap(json, "pocket.json"));
        List<MapFinding> findings = MapValidator.Validate(draft);

        Assert.False(MapValidator.HasErrors(findings));
        Assert.Contains(findings, f => f.Severity == MapSeverity.Warning
                                       && f.Message.Contains("walled off"));
    }
}
