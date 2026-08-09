using Gridfall.Core.Math;

namespace Gridfall.Core.Content;

public enum TargetRule : byte
{
    /// <summary>Furthest along the path. Ties broken by lowest entity id.</summary>
    FurthestAlongPath = 0,
    Nearest = 1,
    LowestAppetite = 2,
}

/// <summary>
/// Immutable, index-addressed. Index is assigned at load by sorting ids
/// alphabetically, so the mapping is stable across runs and machines.
/// Runtime code uses Index; Id is for authoring and logs (engine guide 07).
/// </summary>
/// <summary>
/// One step up the upgrade track. Cost and effect are authored data -- the
/// design rule (rising cost, falling serving-per-gold) patience in the numbers, not
/// in code.
/// </summary>
public sealed class UpgradeLevel
{
    public required int Cost { get; init; }
    public required Fix32 ServingMultiplier { get; init; }
    public required Fix32 RangeMultiplier { get; init; }
    /// <summary>Precomputed at load, like StationDef.RangeSquared.</summary>
    public required Fix32 RangeSquared { get; init; }
    public required int Serving { get; init; }
}

public sealed class StationDef
{
    public required ushort Index { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Cost { get; init; }
    public required Fix32 Range { get; init; }
    /// <summary>Precomputed at load. Never recompute this in the tick loop.</summary>
    public required Fix32 RangeSquared { get; init; }
    public required int Serving { get; init; }
    /// <summary>Ticks, not seconds. Content authors write seconds; the loader converts.</summary>
    public required int CooldownTicks { get; init; }
    public required Fix32 ProjectileSpeed { get; init; }
    public required TargetRule Targeting { get; init; }
    public required int SellValue { get; init; }

    /// <summary>Structure health. Stations are destructible (ADR-0006).</summary>
    public required int Stock { get; init; }

    /// <summary>
    /// Repairing from zero to full costs this percentage of what selling the
    /// station and rebuilding it to the same level would cost.
    ///
    /// Expressed against that alternative rather than against raw spend so the
    /// knob carries its own bound: 100 IS the wall, and a value above it means
    /// nobody would ever repair. Enforced at load, not trusted (ADR-0007).
    /// </summary>
    public required int RepairPercent { get; init; }

    /// <summary>Levels above the base. Empty means the station cannot be upgraded.</summary>
    public required UpgradeLevel[] Upgrades { get; init; }

    public int MaxLevel => Upgrades.Length + 1;

    /// <summary>Serving at a 1-based level.</summary>
    public int ServingAt(int level) => level <= 1 ? Serving : Upgrades[level - 2].Serving;

    /// <summary>Squared range at a 1-based level.</summary>
    public Fix32 RangeSquaredAt(int level) => level <= 1 ? RangeSquared : Upgrades[level - 2].RangeSquared;

    /// <summary>
    /// Half of everything spent to reach this level. Selling can never profit,
    /// however many upgrades were bought.
    /// </summary>
    public int SellValueAt(int level) => TotalSpentAt(level) / 2;

    /// <summary>
    /// What selling actually pays: half of everything spent, scaled by how much
    /// of the station is still standing.
    ///
    /// Selling used to refund the full half regardless of serving, which made
    /// cashing out a nearly-destroyed station strictly better than losing it and
    /// drove stations-destroyed-per-run to zero -- the player pre-empted every
    /// destruction. The value the visitor destroyed is now value the player cannot
    /// recover, which is the whole point of destructible stations.
    ///
    /// An undepleted station returns SellValueAt unchanged, by an explicit early
    /// return rather than by arithmetic that happens to land there. Repositioning
    /// is pillar 1 and must not pay a rounding tax for a feature aimed at wrecks.
    /// </summary>
    public int SalvageValueAt(int level, int hp)
    {
        if (hp >= Stock) return SellValueAt(level);
        if (hp <= 0) return 0;
        return (int)((long)SellValueAt(level) * hp / Stock);
    }

    /// <summary>Base cost plus every upgrade bought to reach this level.</summary>
    public int TotalSpentAt(int level)
    {
        int spent = Cost;
        for (int i = 0; i < level - 1; i++) spent += Upgrades[i].Cost;
        return spent;
    }

    /// <summary>
    /// Gold to restore <paramref name="missingStock"/> points of structure health.
    ///
    /// Anchored to total spend, not base cost, so a level-3 station is
    /// proportionally more expensive to keep alive than a level-1 one. That
    /// maintenance liability is the design's whole interaction with upgrades.
    ///
    /// The 200 in the denominator is 100 (percent) x 2 (the sell refund), so a
    /// full repair costs RepairPercent% of the sell-and-rebuild round trip. The
    /// halving is folded in rather than calling SellValueAt, which would floor
    /// once before this expression floors again.
    ///
    /// Two properties are load-bearing and neither is incidental:
    ///
    /// - The intermediate is <c>long</c>. spent x percent x missingStock reaches
    ///   ~1e9 at plausible values and int overflow is silent. Integer arithmetic
    ///   is exact and therefore deterministic; overflow is exact and therefore
    ///   deterministically WRONG, which is the worse failure.
    /// - Division rounds UP. Truncating would make ten small repairs cheaper
    ///   than one large one -- a free heal for anyone willing to click. Rounding
    ///   up makes granular repair strictly non-advantageous, so the exploit
    ///   closes arithmetically instead of being policed.
    ///
    /// The upper bound on the result is SellValueAt(level): a player who does not
    /// repair can sell for half and rebuild for full, a round trip whose net cost
    /// is exactly that. Above it nobody ever repairs. Enforced at load (ADR-0007).
    /// </summary>
    public int RepairCostFor(int level, int missingStock)
    {
        if (missingStock <= 0) return 0;
        long numerator = (long)TotalSpentAt(level) * RepairPercent * missingStock;
        long denominator = 200L * Stock;
        return (int)((numerator + denominator - 1) / denominator);
    }
}

public sealed class VisitorDef
{
    public required ushort Index { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Appetite { get; init; }
    /// <summary>Cells per tick.</summary>
    public required Fix32 Speed { get; init; }
    public required int Bounty { get; init; }
    public required int PatienceCost { get; init; }

    /// <summary>
    /// Flat serving reduction per HIT. Flat rather than percentage on purpose:
    /// a percentage scales every station equally and changes no decisions, while
    /// flat punishes many-small-hits and rewards few-big-hits. That is the axis
    /// a roster of pure stat-variants cannot express.
    /// </summary>
    public required int Fussiness { get; init; }

    /// <summary>
    /// Serving dealt to a station per attack. Zero means this archetype ignores
    /// stations entirely, which is the default and what every pre-existing
    /// archetype does.
    /// </summary>
    public required int AttackDrain { get; init; }

    public required int AttackCooldownTicks { get; init; }

    /// <summary>Precomputed at load, like StationDef.RangeSquared.</summary>
    public required Fix32 AttackRangeSquared { get; init; }

    public bool AttacksStations => AttackDrain > 0;
}

public sealed class WaveEntry
{
    public required ushort VisitorIndex { get; init; }
    public required int Count { get; init; }
    public required int SpacingTicks { get; init; }
    public required int DelayTicks { get; init; }
    public required int SpawnIndex { get; init; }
}

public sealed class WaveDef
{
    public required int Index { get; init; }

    /// <summary>
    /// Multiplier applied to every visitor's HP in this wave.
    ///
    /// Without this, later waves cannot be harder: visitor HP is fixed per def, so
    /// sending more of the same visitors just hands the player more bounty, which
    /// becomes more stations. Measured -- waves 5-12 leaked nothing at all before
    /// this existed. See 2026-08-06-crossroads-12-waves-balance.md.
    /// </summary>
    public required Fix32 AppetiteScale { get; init; }

    /// <summary>
    /// 0-100. How much the wave's start offsets are jittered, from the table's
    /// `waveVariance`. Zero means the wave plays exactly as authored AND draws
    /// no random numbers at all -- which is what keeps every recorded trace
    /// byte-identical while this is off.
    /// </summary>
    public int VariancePercent { get; init; }

    /// <summary>
    /// Ticks of build time before this wave starts on its own. 0 = no timer,
    /// the wave waits for the player, which is the original behaviour.
    /// </summary>
    public int PrepTicks { get; init; }

    /// <summary>
    /// What a station costs while a wave is running, as a percent of its price.
    /// 100 = no premium. Above 100 makes reacting mid-fight a real decision
    /// rather than the default, and doubles as the scaling gold sink the
    /// economy reports asked for.
    /// </summary>
    public int MidWaveBuildPercent { get; init; } = 100;

    /// <summary>
    /// Gold awarded per whole second of prep time skipped by calling the wave
    /// early. 0 = no bonus. This is what keeps the prep window a decision for a
    /// player who has finished building, instead of a countdown they watch.
    /// </summary>
    public int EarlyCallGoldPerSecond { get; init; }

    /// <summary>
    /// Gold paid for clearing this wave. 0 = bounty only, the original economy.
    ///
    /// The prerequisite for a prep window meaning anything. With bounty-only
    /// income nothing is earned between waves, so a player who has spent down has
    /// no use for the time and the pause is dead -- measured, not assumed.
    /// </summary>
    public int ClearGold { get; init; }
    /// <summary>
    /// Order is load-bearing: entries are walked in array order each tick, so it
    /// determines entity id order on ties. Reordering changes the run.
    /// </summary>
    public required WaveEntry[] Entries { get; init; }
}

public sealed class MapDef
{
    public required string Id { get; init; }

    /// <summary>
    /// Which terrain palette the view should draw this map with. **The simulation
    /// never reads this** -- it is carried here for the same reason StationDef.Name
    /// is: the map file is the one place the author states it, and splitting it
    /// into a side-car would mean two files to keep in step.
    ///
    /// Core deliberately does not know which themes exist. The registry patience in
    /// the view, and a map naming an unknown theme falls back to the default --
    /// caught by a test over the shipped maps rather than by a loader that would
    /// have to hold a list of colours it can never use.
    /// </summary>
    public required string Theme { get; init; }

    public required int Width { get; init; }
    public required int Height { get; init; }
    /// <summary>Row-major, length Width*Height.</summary>
    public required CellKind[] Cells { get; init; }
    /// <summary>Order is content, not layout: the block check iterates this array.</summary>
    public required GridCell[] Spawns { get; init; }
    public required GridCell Goal { get; init; }
    public required int StartingGold { get; init; }
    public required int StartingPatience { get; init; }

    /// <summary>
    /// Which stations this board offers, in the order the toolbar shows them, or
    /// **empty for "all of them"**.
    ///
    /// Empty is the back-compatible default and it is not the same statement as
    /// listing every station: a map that says nothing keeps whatever the content
    /// set grows to, while a map that lists two keeps exactly those two when a
    /// third is added. Boards that mean "these and only these" must say so.
    ///
    /// This IS simulation input, not decoration -- `CommandSystem` refuses a
    /// build that is not on the list, so the toolbar is a view of a rule rather
    /// than a suggestion. It needs no place in the state hash because it never
    /// changes: it is part of the map, like which cells are buildable.
    /// </summary>
    public string[] StationIds { get; init; } = [];

    /// <summary>
    /// Whether this board offers this station. Empty roster means all of them.
    ///
    /// Patience here, on the data, because two callers need the same answer and they
    /// sit on opposite sides of the Core boundary: `CommandSystem` enforces it,
    /// and the view's station bar draws it. A copy in the renderer is how a toolbar
    /// starts offering a station the sim refuses to build.
    /// </summary>
    public bool Offers(ContentSet content, ushort defIndex)
        => StationIds.Length == 0
           || System.Array.IndexOf(StationIds, content.Station(defIndex).Id) >= 0;

    public int Index(GridCell c) => c.Y * Width + c.X;
    public int Index(int x, int y) => y * Width + x;
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public bool InBounds(GridCell c) => InBounds(c.X, c.Y);
}

/// <summary>Everything the sim needs that is not state. Built before the Sim exists.</summary>
public sealed class ContentSet
{
    public required StationDef[] Stations { get; init; }
    public required VisitorDef[] Visitors { get; init; }
    public required WaveDef[] Waves { get; init; }

    public StationDef Station(ushort index) => Stations[index];
    public VisitorDef Visitor(ushort index) => Visitors[index];

    public ushort StationIndexOf(string id)
    {
        for (ushort i = 0; i < Stations.Length; i++)
            if (Stations[i].Id == id) return i;
        throw new KeyNotFoundException($"No station with id '{id}'");
    }

    public ushort VisitorIndexOf(string id)
    {
        for (ushort i = 0; i < Visitors.Length; i++)
            if (Visitors[i].Id == id) return i;
        throw new KeyNotFoundException($"No visitor with id '{id}'");
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

    /// <summary>
    /// The largest spawn-to-goal Manhattan distance the tuned combat model
    /// supports, and therefore the largest board it supports.
    ///
    /// **Why the path band is absolute and not a fraction of the board.** The
    /// 18-30 band is about time under fire -- how many cells a visitor is exposed
    /// for, against station DPS -- not about geometry. Scaling it with board size
    /// would keep the warning quiet on a 64x64 map while quietly claiming a
    /// combat model nothing has tested.
    ///
    /// So the band stays put and this states its consequence out loud: a board
    /// whose spawn and goal are further apart than this cannot satisfy the band
    /// no matter how it is painted, because the Manhattan distance IS the
    /// shortest route any such map can have.
    /// </summary>
    public const int MaxSpawnGoalDistance = MaxUnmazedPath;
}
