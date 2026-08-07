using System.Text.Json;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Core.Content;

public sealed class ContentException : Exception
{
    public ContentException(string message) : base(message) { }
}

/// <summary>
/// JSON in content-data/ becomes runtime data here, and nowhere else.
///
/// Core never touches the filesystem: the loader runs before the Sim is
/// constructed and hands it a finished ContentSet. That is what lets the harness
/// build content in memory and the game load it from disk without the sim
/// knowing which happened (engine guide 07).
/// </summary>
public static class ContentLoader
{
    public const int TicksPerSecond = 30;

    // ---- number parsing ---------------------------------------------------

    /// <summary>
    /// The single conversion point from an authored decimal to a sim value.
    /// GetDouble() is never called: parsing to double and scaling reintroduces
    /// the platform-dependent rounding Fix32 exists to avoid.
    /// </summary>
    public static Fix32 ParseFix(JsonElement element, string path)
    {
        string raw = element.GetRawText().Trim();
        var (numerator, denominator) = DecimalToRational(raw, path);
        return Fix32.FromFraction(numerator, denominator);
    }

    internal static (int numerator, int denominator) DecimalToRational(string text, string path)
    {
        bool negative = false;
        int i = 0;
        if (i < text.Length && (text[i] == '-' || text[i] == '+'))
        {
            negative = text[i] == '-';
            i++;
        }

        long whole = 0;
        int digits = 0;
        for (; i < text.Length && text[i] >= '0' && text[i] <= '9'; i++, digits++)
            whole = whole * 10 + (text[i] - '0');

        long fracNumerator = 0;
        long fracDenominator = 1;
        if (i < text.Length && text[i] == '.')
        {
            i++;
            for (; i < text.Length && text[i] >= '0' && text[i] <= '9'; i++)
            {
                if (fracDenominator > 100_000_000) break; // enough precision; ignore the tail
                fracNumerator = fracNumerator * 10 + (text[i] - '0');
                fracDenominator *= 10;
            }
        }

        if (digits == 0 && fracDenominator == 1)
            throw new ContentException($"{path}: '{text}' is not a number");

        long numerator = whole * fracDenominator + fracNumerator;
        if (negative) numerator = -numerator;

        if (numerator > int.MaxValue || numerator < int.MinValue)
            throw new ContentException($"{path}: value '{text}' is out of range for Fix32");

        return ((int)numerator, (int)fracDenominator);
    }

    /// <summary>Seconds in the data file, ticks in the sim. Rounded once, here.</summary>
    public static int SecondsToTicks(JsonElement element, string path)
    {
        var (num, den) = DecimalToRational(element.GetRawText().Trim(), path);
        // round-half-up in integer arithmetic
        long ticks = ((long)num * TicksPerSecond * 2 + den) / (den * 2);
        if (ticks < 1) ticks = 1;
        return (int)ticks;
    }

    // ---- towers -----------------------------------------------------------

    public static TowerDef[] LoadTowers(IEnumerable<(string name, string json)> files)
    {
        var list = new List<(string id, string name, JsonDocument doc)>();
        foreach (var (name, json) in files)
        {
            JsonDocument doc = Parse(json, name);
            string id = RequireString(doc.RootElement, "id", name);
            list.Add((id, name, doc));
        }

        // Index is assigned in sorted-id order so it is stable across machines,
        // whatever order the filesystem hands us the files in.
        list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        var towers = new TowerDef[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var (id, file, doc) = list[i];
            JsonElement r = doc.RootElement;

            int cost = RequireInt(r, "cost", file);
            if (cost <= 0) throw new ContentException($"{file}: cost must be > 0");
            Fix32 range = ParseFix(RequireProperty(r, "range", file), file);
            if (range <= Fix32.Zero) throw new ContentException($"{file}: range must be > 0");
            int damage = RequireInt(r, "damage", file);
            if (damage < 0) throw new ContentException($"{file}: damage must be >= 0");
            int cooldown = SecondsToTicks(RequireProperty(r, "cooldown", file), file);

            // Every tower is repairable unless a future design says otherwise, so
            // this defaults rather than opting in the way an enemy's attackDamage
            // does. ADR-0007 records why "unrepairable" must eventually be its own
            // field instead of a cost nobody would pay.
            int repairPercent = r.TryGetProperty("repairPercent", out var rp) ? rp.GetInt32() : 60;
            if (repairPercent is <= 0 or >= 100)
                throw new ContentException(
                    $"{file}: repairPercent must be between 1 and 99 (got {repairPercent}). " +
                    "It is a percentage of the sell-and-rebuild cost, so 100 IS the wall: " +
                    "at or above it nobody would ever repair.");

            towers[i] = new TowerDef
            {
                Index = (ushort)i,
                Id = id,
                Name = RequireString(r, "name", file),
                Cost = cost,
                Range = range,
                RangeSquared = range * range,
                Damage = damage,
                CooldownTicks = cooldown,
                ProjectileSpeed = r.TryGetProperty("projectileSpeed", out var ps)
                    ? ParseFix(ps, file)
                    : Fix32.FromInt(1),
                Targeting = ParseTargeting(r, file),
                SellValue = r.TryGetProperty("sellValue", out var sv) ? sv.GetInt32() : cost / 2,
                Hp = r.TryGetProperty("hp", out var thp) ? thp.GetInt32() : 100,
                RepairPercent = repairPercent,
                Upgrades = ParseUpgrades(r, file, damage, range),
            };
            doc.Dispose();

            ValidateRepairCurve(towers[i], file);
        }
        return towers;
    }

    /// <summary>
    /// The repair cost bound, enforced rather than trusted (ADR-0007).
    ///
    /// repairPercent and the cost fields live in different places and either can
    /// be edited without looking at the other. When they conflict the mechanic
    /// does not crash -- it just becomes arithmetically dominated by
    /// sell-and-rebuild, and the next balance report reads as "players don't
    /// repair much" rather than "repair is impossible to justify".
    ///
    /// The loader is the only place underneath all three consumers: the game,
    /// the board editor, and the balance sim.
    /// </summary>
    private static void ValidateRepairCurve(TowerDef def, string file)
    {
        for (int level = 1; level <= def.MaxLevel; level++)
        {
            int toFull = def.RepairCostFor(level, def.Hp);
            int sellRebuild = def.SellValueAt(level);
            if (toFull < sellRebuild) continue;

            throw new ContentException(
                $"{file}: repairing '{def.Id}' at level {level} from zero costs {toFull} gold, " +
                $"but selling and rebuilding it costs only {sellRebuild}. " +
                $"Nobody would ever repair. Lower repairPercent (currently {def.RepairPercent}) " +
                $"or raise the level-{level} cost (total spent {def.TotalSpentAt(level)}).");
        }
    }

    private static Fix32 AttackRange(JsonElement r, string file)
    {
        Fix32 range = r.TryGetProperty("attackRange", out JsonElement ar)
            ? ParseFix(ar, file)
            : Fix32.FromFraction(12, 10);   // just over one cell: adjacent towers
        return range * range;
    }

    private static UpgradeLevel[] ParseUpgrades(JsonElement r, string file, int baseDamage, Fix32 baseRange)
    {
        if (!r.TryGetProperty("upgrades", out JsonElement arr)) return Array.Empty<UpgradeLevel>();

        var levels = new List<UpgradeLevel>();
        foreach (JsonElement u in arr.EnumerateArray())
        {
            int cost = RequireInt(u, "cost", file);
            if (cost <= 0) throw new ContentException($"{file}: upgrade cost must be > 0");

            Fix32 dmgMul = ParseFix(RequireProperty(u, "damageMultiplier", file), file);
            if (dmgMul < Fix32.One)
                throw new ContentException($"{file}: damageMultiplier {dmgMul} would weaken the tower");

            Fix32 rangeMul = u.TryGetProperty("rangeMultiplier", out JsonElement rm)
                ? ParseFix(rm, file) : Fix32.One;

            // Resolved once here, so the tick loop never multiplies to find a
            // tower's damage or range -- same reason RangeSquared is precomputed.
            Fix32 range = baseRange * rangeMul;
            levels.Add(new UpgradeLevel
            {
                Cost = cost,
                DamageMultiplier = dmgMul,
                RangeMultiplier = rangeMul,
                RangeSquared = range * range,
                Damage = (int)(((long)baseDamage * dmgMul.Raw) >> Fix32.FractionalBits),
            });
        }
        return levels.ToArray();
    }

    private static TargetRule ParseTargeting(JsonElement r, string file)
    {
        if (!r.TryGetProperty("targeting", out var t)) return TargetRule.FurthestAlongPath;
        return t.GetString() switch
        {
            "furthest-along-path" => TargetRule.FurthestAlongPath,
            "nearest" => TargetRule.Nearest,
            "lowest-hp" => TargetRule.LowestHp,
            var other => throw new ContentException($"{file}: unknown targeting rule '{other}'"),
        };
    }

    // ---- enemies ----------------------------------------------------------

    public static EnemyDef[] LoadEnemies(IEnumerable<(string name, string json)> files)
    {
        var list = new List<(string id, string name, JsonDocument doc)>();
        foreach (var (name, json) in files)
        {
            JsonDocument doc = Parse(json, name);
            list.Add((RequireString(doc.RootElement, "id", name), name, doc));
        }
        list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        var enemies = new EnemyDef[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var (id, file, doc) = list[i];
            JsonElement r = doc.RootElement;

            int hp = RequireInt(r, "hp", file);
            if (hp <= 0) throw new ContentException($"{file}: hp must be > 0");
            Fix32 speed = ParseFix(RequireProperty(r, "speed", file), file);
            if (speed <= Fix32.Zero) throw new ContentException($"{file}: speed must be > 0");

            enemies[i] = new EnemyDef
            {
                Index = (ushort)i,
                Id = id,
                Name = RequireString(r, "name", file),
                Hp = hp,
                Speed = speed,
                Bounty = RequireInt(r, "bounty", file),
                LivesCost = r.TryGetProperty("livesCost", out var lc) ? lc.GetInt32() : 1,
                Armour = r.TryGetProperty("armour", out var ar) ? ar.GetInt32() : 0,
                AttackDamage = r.TryGetProperty("attackDamage", out var ad) ? ad.GetInt32() : 0,
                AttackCooldownTicks = r.TryGetProperty("attackCooldown", out var ac)
                    ? SecondsToTicks(ac, file) : 30,
                AttackRangeSquared = AttackRange(r, file),
            };
            doc.Dispose();
        }
        return enemies;
    }

    // ---- waves ------------------------------------------------------------

    public static WaveDef[] LoadWaves(string json, EnemyDef[] enemies, string file)
    {
        using JsonDocument doc = Parse(json, file);
        JsonElement wavesEl = RequireProperty(doc.RootElement, "waves", file);

        // One authored growth rate, compounded here rather than in the tick loop.
        // The balance targets want 1.10-1.18x wave to wave, so the content states
        // the rate and the loader turns it into a per-wave scalar.
        Fix32 growth = doc.RootElement.TryGetProperty("hpGrowth", out JsonElement g)
            ? ParseFix(g, file)
            : Fix32.One;

        if (growth < Fix32.One)
            throw new ContentException($"{file}: hpGrowth {growth} would make later waves weaker");

        // The wave the ramp starts from. Waves at or before it sit at scale 1.0,
        // so the opening is flat and the curve steepens afterwards.
        //
        // One knob could not do this. hpGrowth applies from wave 1, so the only
        // way to threaten wave 12 was a rate that also inflated waves 2-4 -- and
        // waves 2-4 are where the player is broke, so they were the binding
        // constraint on the whole curve. Six passes pushed that single scalar and
        // every one of them had to choose between a lethal opening and a trivial
        // ending. See 2026-08-07-early-economy-2-balance.md.
        //
        // Defaults to 1, which is exactly the previous behaviour: growth^(index-1).
        int growthFrom = doc.RootElement.TryGetProperty("hpGrowthFrom", out JsonElement gf)
            ? gf.GetInt32()
            : 1;

        if (growthFrom < 1)
            throw new ContentException($"{file}: hpGrowthFrom {growthFrom} must be at least 1");

        var waves = new List<WaveDef>();
        foreach (JsonElement w in wavesEl.EnumerateArray())
        {
            int index = RequireInt(w, "index", file);
            var entries = new List<WaveEntry>();
            foreach (JsonElement e in RequireProperty(w, "entries", file).EnumerateArray())
            {
                string enemyId = RequireString(e, "enemy", file);
                ushort enemyIndex = ushort.MaxValue;
                for (ushort k = 0; k < enemies.Length; k++)
                    if (enemies[k].Id == enemyId) { enemyIndex = k; break; }
                if (enemyIndex == ushort.MaxValue)
                    throw new ContentException($"{file}: wave {index} names unknown enemy '{enemyId}'");

                entries.Add(new WaveEntry
                {
                    EnemyIndex = enemyIndex,
                    Count = RequireInt(e, "count", file),
                    SpacingTicks = RequireInt(e, "spacingTicks", file),
                    DelayTicks = e.TryGetProperty("delayTicks", out var d) ? d.GetInt32() : 0,
                    SpawnIndex = e.TryGetProperty("spawn", out var s) ? s.GetInt32() : 0,
                });
            }
            // Compounded with Fix32 multiply so the scalar is bit-identical
            // everywhere -- Math.Pow would put a double in the content path.
            Fix32 scale = Fix32.One;
            for (int i = growthFrom; i < index; i++) scale *= growth;

            if (w.TryGetProperty("hpScale", out JsonElement explicitScale))
                scale = ParseFix(explicitScale, file);

            waves.Add(new WaveDef { Index = index, Entries = entries.ToArray(), HpScale = scale });
        }

        waves.Sort((a, b) => a.Index.CompareTo(b.Index));
        return waves.ToArray();
    }

    // ---- maps -------------------------------------------------------------

    public static MapDef LoadMap(string json, string file)
    {
        using JsonDocument doc = Parse(json, file);
        JsonElement r = doc.RootElement;

        string id = RequireString(r, "id", file);
        int width = RequireInt(r, "width", file);
        int height = RequireInt(r, "height", file);
        if (width is < 8 or > 64) throw new ContentException($"{file}: width {width} outside 8..64");
        if (height is < 8 or > 64) throw new ContentException($"{file}: height {height} outside 8..64");

        JsonElement rows = RequireProperty(r, "cells", file);
        int rowCount = rows.GetArrayLength();
        if (rowCount != height)
            throw new ContentException($"{file}: cells has {rowCount} rows, height says {height}");

        var cells = new CellKind[width * height];
        var glyphSpawns = new List<GridCell>();
        GridCell glyphGoal = GridCell.Invalid;
        int y = 0;
        foreach (JsonElement rowEl in rows.EnumerateArray())
        {
            string row = rowEl.GetString() ?? throw new ContentException($"{file}: cells row {y} is not a string");
            if (row.Length != width)
                throw new ContentException($"{file}: cells row {y} is {row.Length} wide, width says {width}");

            for (int x = 0; x < width; x++)
            {
                CellKind kind = row[x] switch
                {
                    '.' => CellKind.PathOnly,
                    'b' => CellKind.Buildable,
                    '#' => CellKind.Blocked,
                    'S' => CellKind.Spawn,
                    'G' => CellKind.Goal,
                    var c => throw new ContentException($"{file}: unknown map glyph '{c}' at ({x},{y})"),
                };
                cells[y * width + x] = kind;
                if (kind == CellKind.Spawn) glyphSpawns.Add(new GridCell(x, y));
                if (kind == CellKind.Goal)
                {
                    if (glyphGoal.IsValid) throw new ContentException($"{file}: more than one goal");
                    glyphGoal = new GridCell(x, y);
                }
            }
            y++;
        }

        if (!glyphGoal.IsValid) throw new ContentException($"{file}: map has no goal");
        if (glyphSpawns.Count == 0) throw new ContentException($"{file}: map has no spawn");

        // Spawn ORDER comes from the spawns array, not the glyph scan -- anything
        // that iterates spawns uses it, so it is a content decision.
        GridCell[] spawns;
        if (r.TryGetProperty("spawns", out JsonElement spawnsEl))
        {
            var listed = new List<GridCell>();
            foreach (JsonElement s in spawnsEl.EnumerateArray())
                listed.Add(new GridCell(RequireInt(s, "x", file), RequireInt(s, "y", file)));

            foreach (GridCell c in listed)
                if (!glyphSpawns.Contains(c))
                    throw new ContentException($"{file}: spawns lists {c}, which is not an 'S' cell");
            foreach (GridCell c in glyphSpawns)
                if (!listed.Contains(c))
                    throw new ContentException($"{file}: 'S' cell {c} is missing from the spawns array");

            spawns = listed.ToArray();
        }
        else
        {
            spawns = glyphSpawns.ToArray();
        }

        var draft = new MapDraft
        {
            Id = id,
            // View-only, and Core holds no list of valid themes -- an unknown one
            // falls back in the renderer rather than failing the load, because a
            // map that will not open is a worse failure than a map drawn in the
            // default palette.
            Theme = r.TryGetProperty("theme", out var th) ? th.GetString() ?? "slate" : "slate",
            Width = width,
            Height = height,
            Cells = cells,
            Goal = glyphGoal,
            StartingGold = r.TryGetProperty("startingGold", out var sg) ? sg.GetInt32() : 200,
            StartingLives = r.TryGetProperty("startingLives", out var sl) ? sl.GetInt32() : 20,
        };
        draft.Spawns.AddRange(spawns);

        // ONE verdict. The board editor calls this same validator live as you
        // paint, so it can never disagree with the loader about what is legal.
        foreach (MapFinding finding in MapValidator.Validate(draft))
            if (finding.Severity == MapSeverity.Error)
                throw new ContentException($"{file}: {finding}");

        return draft.ToMapDef();
    }

    // ---- helpers ----------------------------------------------------------

    private static JsonDocument Parse(string json, string file)
    {
        try { return JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new ContentException($"{file}: invalid JSON -- {ex.Message}"); }
    }

    private static JsonElement RequireProperty(JsonElement e, string name, string file)
        => e.TryGetProperty(name, out JsonElement v)
            ? v
            : throw new ContentException($"{file}: missing required field '{name}'");

    private static string RequireString(JsonElement e, string name, string file)
        => RequireProperty(e, name, file).GetString()
           ?? throw new ContentException($"{file}: field '{name}' is not a string");

    private static int RequireInt(JsonElement e, string name, string file)
    {
        JsonElement v = RequireProperty(e, name, file);
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int result))
            throw new ContentException($"{file}: field '{name}' is not an integer");
        return result;
    }
}
