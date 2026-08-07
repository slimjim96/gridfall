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
/// <summary>
/// One step up the upgrade track. Cost and effect are authored data -- the
/// design rule (rising cost, falling damage-per-gold) lives in the numbers, not
/// in code.
/// </summary>
public sealed class UpgradeLevel
{
    public required int Cost { get; init; }
    public required Fix32 DamageMultiplier { get; init; }
    public required Fix32 RangeMultiplier { get; init; }
    /// <summary>Precomputed at load, like TowerDef.RangeSquared.</summary>
    public required Fix32 RangeSquared { get; init; }
    public required int Damage { get; init; }
}

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

    /// <summary>Levels above the base. Empty means the tower cannot be upgraded.</summary>
    public required UpgradeLevel[] Upgrades { get; init; }

    public int MaxLevel => Upgrades.Length + 1;

    /// <summary>Damage at a 1-based level.</summary>
    public int DamageAt(int level) => level <= 1 ? Damage : Upgrades[level - 2].Damage;

    /// <summary>Squared range at a 1-based level.</summary>
    public Fix32 RangeSquaredAt(int level) => level <= 1 ? RangeSquared : Upgrades[level - 2].RangeSquared;

    /// <summary>
    /// Half of everything spent to reach this level. Selling can never profit,
    /// however many upgrades were bought.
    /// </summary>
    public int SellValueAt(int level)
    {
        int spent = Cost;
        for (int i = 0; i < level - 1; i++) spent += Upgrades[i].Cost;
        return spent / 2;
    }
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

    /// <summary>
    /// Flat damage reduction per HIT. Flat rather than percentage on purpose:
    /// a percentage scales every tower equally and changes no decisions, while
    /// flat punishes many-small-hits and rewards few-big-hits. That is the axis
    /// a roster of pure stat-variants cannot express.
    /// </summary>
    public required int Armour { get; init; }
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
    /// Multiplier applied to every enemy's HP in this wave.
    ///
    /// Without this, later waves cannot be harder: enemy HP is fixed per def, so
    /// sending more of the same creeps just hands the player more bounty, which
    /// becomes more towers. Measured -- waves 5-12 leaked nothing at all before
    /// this existed. See 2026-08-06-crossroads-12-waves-balance.md.
    /// </summary>
    public required Fix32 HpScale { get; init; }
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
