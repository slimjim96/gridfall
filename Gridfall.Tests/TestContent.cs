using Gridfall.Core;
using Gridfall.Core.Content;

namespace Gridfall.Tests;

/// <summary>
/// Fixture maps and content, built in memory. Core never touches the
/// filesystem, so tests build a ContentSet the same way the game does.
/// </summary>
public static class TestContent
{
    /// <summary>A straight corridor. Nothing buildable -- pure movement.</summary>
    public const string CorridorMap = """
    {
      "id": "corridor", "width": 12, "height": 8,
      "cells": [
        "############",
        "#..........#",
        "#..........#",
        "S..........G",
        "#..........#",
        "#..........#",
        "#..........#",
        "############"
      ],
      "spawns": [{ "x": 0, "y": 3 }],
      "startingGold": 500, "startingLives": 20
    }
    """;

    /// <summary>
    /// One buildable cell is the only gap in a wall. Building there would seal
    /// the lane, so the block check must refuse it.
    /// </summary>
    public const string PinchMap = """
    {
      "id": "pinch", "width": 12, "height": 8,
      "cells": [
        "############",
        "#####.######",
        "#####b######",
        "S....b.....G",
        "#####b######",
        "#####.######",
        "############",
        "############"
      ],
      "spawns": [{ "x": 0, "y": 3 }],
      "startingGold": 500, "startingLives": 20
    }
    """;

    /// <summary>
    /// Two routes of equal length that are NOT mirror images: a dogleg north and
    /// a dogleg south of differing shape. A symmetric map cannot catch a
    /// tie-break bug, which is exactly how the flow-field overwrite defect hid
    /// in the worked example.
    /// </summary>
    public const string DoglegTieMap = """
    {
      "id": "dogleg-tie", "width": 12, "height": 9,
      "cells": [
        "############",
        "#..........#",
        "#.########.#",
        "#.########.#",
        "S.########.G",
        "#.########.#",
        "#.########.#",
        "#..........#",
        "############"
      ],
      "spawns": [{ "x": 0, "y": 4 }],
      "startingGold": 500, "startingLives": 20
    }
    """;

    /// <summary>An open board where towers can be placed next to the lane.</summary>
    public const string ArenaMap = """
    {
      "id": "arena", "width": 12, "height": 9,
      "cells": [
        "############",
        "#bbbbbbbbbb#",
        "#bbbbbbbbbb#",
        "#bbbbbbbbbb#",
        "S..........G",
        "#bbbbbbbbbb#",
        "#bbbbbbbbbb#",
        "#bbbbbbbbbb#",
        "############"
      ],
      "spawns": [{ "x": 0, "y": 4 }],
      "startingGold": 500, "startingLives": 20
    }
    """;

    /// <summary>
    /// The lane itself is buildable, so a tower can actually lengthen the route.
    ///
    /// ArenaMap cannot do this: its lane is path-only, so no legal build changes
    /// the route at all -- which made a preview test pass for the wrong reason.
    /// </summary>
    public const string LaneMap = """
    {
      "id": "lane", "width": 12, "height": 8,
      "cells": [
        "############",
        "#bbbbbbbbbb#",
        "#bbbbbbbbbb#",
        "SbbbbbbbbbbG",
        "#bbbbbbbbbb#",
        "#bbbbbbbbbb#",
        "#bbbbbbbbbb#",
        "############"
      ],
      "spawns": [{ "x": 0, "y": 3 }],
      "startingGold": 500, "startingLives": 20
    }
    """;

    // Mirrors the shipped shape, upgrades included -- a fixture without them
    // makes every upgrade test pass vacuously by being refused at max level.
    private const string ArrowTower = """
    { "id": "arrow-tower", "name": "Arrow Tower", "cost": 50, "range": 3.0,
      "cooldown": 0.6, "damage": 12, "projectileSpeed": 0.8,
      "targeting": "furthest-along-path", "sellValue": 25,
      "upgrades": [
        { "cost": 110, "damageMultiplier": 2.0, "rangeMultiplier": 1.0 },
        { "cost": 240, "damageMultiplier": 4.0, "rangeMultiplier": 1.15 } ] }
    """;

    private const string Cannon = """
    { "id": "cannon", "name": "Cannon", "cost": 90, "range": 2.5,
      "cooldown": 1.5, "damage": 40, "projectileSpeed": 0.5,
      "targeting": "furthest-along-path", "sellValue": 45,
      "upgrades": [
        { "cost": 198, "damageMultiplier": 2.0, "rangeMultiplier": 1.0 },
        { "cost": 432, "damageMultiplier": 4.0, "rangeMultiplier": 1.15 } ] }
    """;

    /// <summary>One shot kills a runner. Used for the simultaneous-kill test.</summary>
    private const string Sniper = """
    { "id": "sniper", "name": "Sniper", "cost": 10, "range": 6.0,
      "cooldown": 0.2, "damage": 1000, "projectileSpeed": 6.0,
      "targeting": "furthest-along-path", "sellValue": 5 }
    """;

    private const string Runner = """
    { "id": "runner", "name": "Runner", "hp": 60, "speed": 0.06, "bounty": 8, "livesCost": 1 }
    """;

    private const string Brute = """
    { "id": "brute", "name": "Brute", "hp": 220, "speed": 0.03, "bounty": 20, "livesCost": 2 }
    """;

    private const string Waves = """
    {
      "map": "test",
      "waves": [
        { "index": 1, "entries": [ { "enemy": "runner", "count": 4, "spacingTicks": 20, "spawn": 0 } ] },
        { "index": 2, "entries": [
            { "enemy": "runner", "count": 6, "spacingTicks": 15, "spawn": 0 },
            { "enemy": "brute",  "count": 2, "spacingTicks": 40, "delayTicks": 60, "spawn": 0 } ] }
      ]
    }
    """;

    public static ContentSet BuildContent()
    {
        TowerDef[] towers = ContentLoader.LoadTowers(new[]
        {
            ("arrow-tower.json", ArrowTower),
            ("cannon.json", Cannon),
            ("sniper.json", Sniper),
        });
        EnemyDef[] enemies = ContentLoader.LoadEnemies(new[]
        {
            ("runner.json", Runner),
            ("brute.json", Brute),
        });
        WaveDef[] waves = ContentLoader.LoadWaves(Waves, enemies, "waves.json");
        return new ContentSet { Towers = towers, Enemies = enemies, Waves = waves };
    }

    public static MapDef Map(string json, string name = "fixture") => ContentLoader.LoadMap(json, name);

    public static Sim NewSim(string mapJson, uint seed = 1)
        => new(Map(mapJson), BuildContent(), seed);
}
