using Gridfall.Core.Math;

namespace Gridfall.Core.Content;

public enum TargetRule : byte
{
    /// <summary>Furthest along the path. Ties broken by lowest entity id.</summary>
    FurthestAlongPath = 0,
    Nearest = 1,
    LowestHp = 2,
}

/// <summary>
/// Immutable, index-addressed. Index is assigned at load by sorting ids
/// alphabetically, so the mapping is stable across runs and machines.
/// Runtime code uses Index; Id is for authoring and logs (engine guide 07).
/// </summary>
public sealed class TowerDef
{
    public required ushort Index { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Cost { get; init; }
    public required Fix32 Range { get; init; }
    /// <summary>Precomputed at load. Never recompute this in the tick loop.</summary>
    public required Fix32 RangeSquared { get; init; }
    public required int Damage { get; init; }
    /// <summary>Ticks, not seconds. Content authors write seconds; the loader converts.</summary>
    public required int CooldownTicks { get; init; }
    public required Fix32 ProjectileSpeed { get; init; }
    public required TargetRule Targeting { get; init; }
    public required int SellValue { get; init; }
}

public sealed class EnemyDef
{
    public required ushort Index { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Hp { get; init; }
    /// <summary>Cells per tick.</summary>
    public required Fix32 Speed { get; init; }
    public required int Bounty { get; init; }
    public required int LivesCost { get; init; }
}

public sealed class WaveEntry
{
    public required ushort EnemyIndex { get; init; }
    public required int Count { get; init; }
    public required int SpacingTicks { get; init; }
    public required int DelayTicks { get; init; }
    public required int SpawnIndex { get; init; }
}

public sealed class WaveDef
{
    public required int Index { get; init; }
    /// <summary>
    /// Order is load-bearing: entries are walked in array order each tick, so it
    /// determines entity id order on ties. Reordering changes the run.
    /// </summary>
    public required WaveEntry[] Entries { get; init; }
}

public sealed class MapDef
{
    public required string Id { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    /// <summary>Row-major, length Width*Height.</summary>
    public required CellKind[] Cells { get; init; }
    /// <summary>Order is content, not layout: the block check iterates this array.</summary>
    public required GridCell[] Spawns { get; init; }
    public required GridCell Goal { get; init; }
    public required int StartingGold { get; init; }
    public required int StartingLives { get; init; }

    public int Index(GridCell c) => c.Y * Width + c.X;
    public int Index(int x, int y) => y * Width + x;
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public bool InBounds(GridCell c) => InBounds(c.X, c.Y);
}

/// <summary>Everything the sim needs that is not state. Built before the Sim exists.</summary>
public sealed class ContentSet
{
    public required TowerDef[] Towers { get; init; }
    public required EnemyDef[] Enemies { get; init; }
    public required WaveDef[] Waves { get; init; }

    public TowerDef Tower(ushort index) => Towers[index];
    public EnemyDef Enemy(ushort index) => Enemies[index];

    public ushort TowerIndexOf(string id)
    {
        for (ushort i = 0; i < Towers.Length; i++)
            if (Towers[i].Id == id) return i;
        throw new KeyNotFoundException($"No tower with id '{id}'");
    }

    public ushort EnemyIndexOf(string id)
    {
        for (ushort i = 0; i < Enemies.Length; i++)
            if (Enemies[i].Id == id) return i;
        throw new KeyNotFoundException($"No enemy with id '{id}'");
    }
}

/// <summary>
/// The map thresholds from content-data/docs/balance-targets.md, in code, once.
/// The balance sim's map report and the board editor's validation panel both
/// read these -- so they cannot disagree. Change a number here and in that doc
/// together.
/// </summary>
public static class MapTargets
{
    public const int MinUnmazedPath = 18;
    public const int MaxUnmazedPath = 30;
    public const int MinBuildablePercent = 35;
    public const int MaxBuildablePercent = 55;
    public const int MaxLanes = 3;
    /// <summary>Longest mazed path as a multiple of the unmazed path.</summary>
    public const int MaxMazeMultiplier = 3;
}
