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

    // ---- stations -----------------------------------------------------------

    public static StationDef[] LoadStations(IEnumerable<(string name, string json)> files)
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

        var stations = new StationDef[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var (id, file, doc) = list[i];
            JsonElement r = doc.RootElement;

            int cost = RequireInt(r, "cost", file);
            if (cost <= 0) throw new ContentException($"{file}: cost must be > 0");
            Fix32 range = ParseFix(RequireProperty(r, "range", file), file);
            if (range <= Fix32.Zero) throw new ContentException($"{file}: range must be > 0");
            int serving = RequireInt(r, "serving", file);
            if (serving < 0) throw new ContentException($"{file}: serving must be >= 0");
            int cooldown = SecondsToTicks(RequireProperty(r, "cooldown", file), file);

            // Every station is repairable unless a future design says otherwise, so
            // this defaults rather than opting in the way an visitor's attackDrain
            // does. ADR-0007 records why "unrepairable" must eventually be its own
            // field instead of a cost nobody would pay.
            int repairPercent = r.TryGetProperty("repairPercent", out var rp) ? rp.GetInt32() : 60;
            if (repairPercent is <= 0 or >= 100)
                throw new ContentException(
                    $"{file}: repairPercent must be between 1 and 99 (got {repairPercent}). " +
                    "It is a percentage of the sell-and-rebuild cost, so 100 IS the wall: " +
                    "at or above it nobody would ever repair.");

            stations[i] = new StationDef
            {
                Index = (ushort)i,
                Id = id,
                Name = RequireString(r, "name", file),
                Cost = cost,
                Range = range,
                RangeSquared = range * range,
                Serving = serving,
                CooldownTicks = cooldown,
                ProjectileSpeed = r.TryGetProperty("projectileSpeed", out var ps)
                    ? ParseFix(ps, file)
                    : Fix32.FromInt(1),
                Targeting = ParseTargeting(r, file),
                SellValue = r.TryGetProperty("sellValue", out var sv) ? sv.GetInt32() : cost / 2,
                Stock = r.TryGetProperty("stock", out var thp) ? thp.GetInt32() : 100,
                RepairPercent = repairPercent,
                Upgrades = ParseUpgrades(r, file, serving, range),
            };
            doc.Dispose();

            ValidateRepairCurve(stations[i], file);
        }
        return stations;
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
    private static void ValidateRepairCurve(StationDef def, string file)
    {
        for (int level = 1; level <= def.MaxLevel; level++)
        {
            int toFull = def.RepairCostFor(level, def.Stock);
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
            : Fix32.FromFraction(12, 10);   // just over one cell: adjacent stations
        return range * range;
    }

    private static UpgradeLevel[] ParseUpgrades(JsonElement r, string file, int baseServing, Fix32 baseRange)
    {
        if (!r.TryGetProperty("upgrades", out JsonElement arr)) return Array.Empty<UpgradeLevel>();

        var levels = new List<UpgradeLevel>();
        foreach (JsonElement u in arr.EnumerateArray())
        {
            int cost = RequireInt(u, "cost", file);
            if (cost <= 0) throw new ContentException($"{file}: upgrade cost must be > 0");

            Fix32 dmgMul = ParseFix(RequireProperty(u, "servingMultiplier", file), file);
            if (dmgMul < Fix32.One)
                throw new ContentException($"{file}: servingMultiplier {dmgMul} would weaken the station");

            Fix32 rangeMul = u.TryGetProperty("rangeMultiplier", out JsonElement rm)
                ? ParseFix(rm, file) : Fix32.One;

            // Resolved once here, so the tick loop never multiplies to find a
            // station's serving or range -- same reason RangeSquared is precomputed.
            Fix32 range = baseRange * rangeMul;
            levels.Add(new UpgradeLevel
            {
                Cost = cost,
                ServingMultiplier = dmgMul,
                RangeMultiplier = rangeMul,
                RangeSquared = range * range,
                Serving = (int)(((long)baseServing * dmgMul.Raw) >> Fix32.FractionalBits),
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
            "lowest-hp" => TargetRule.LowestAppetite,
            var other => throw new ContentException($"{file}: unknown targeting rule '{other}'"),
        };
    }

    // ---- visitors ----------------------------------------------------------

    public static VisitorDef[] LoadVisitors(IEnumerable<(string name, string json)> files)
    {
        var list = new List<(string id, string name, JsonDocument doc)>();
        foreach (var (name, json) in files)
        {
            JsonDocument doc = Parse(json, name);
            list.Add((RequireString(doc.RootElement, "id", name), name, doc));
        }
        list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        var visitors = new VisitorDef[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var (id, file, doc) = list[i];
            JsonElement r = doc.RootElement;

            int appetite = RequireInt(r, "appetite", file);
            if (appetite <= 0) throw new ContentException($"{file}: appetite must be > 0");
            Fix32 speed = ParseFix(RequireProperty(r, "speed", file), file);
            if (speed <= Fix32.Zero) throw new ContentException($"{file}: speed must be > 0");

            visitors[i] = new VisitorDef
            {
                Index = (ushort)i,
                Id = id,
                Name = RequireString(r, "name", file),
                Appetite = appetite,
                Speed = speed,
                Bounty = RequireInt(r, "bounty", file),
                PatienceCost = r.TryGetProperty("patienceCost", out var lc) ? lc.GetInt32() : 1,
                Fussiness = r.TryGetProperty("fussiness", out var ar) ? ar.GetInt32() : 0,
                AttackDrain = r.TryGetProperty("attackDrain", out var ad) ? ad.GetInt32() : 0,
                AttackCooldownTicks = r.TryGetProperty("attackCooldown", out var ac)
                    ? SecondsToTicks(ac, file) : 30,
                AttackRangeSquared = AttackRange(r, file),
            };
            doc.Dispose();
        }
        return visitors;
    }

    // ---- waves ------------------------------------------------------------

    /// <summary>An optional int with a documented range. Out of range throws rather than clamps.</summary>
    private static int OptionalInt(JsonElement root, string name, int fallback, int min, int max, string file)
    {
        if (!root.TryGetProperty(name, out JsonElement e)) return fallback;
        int value = e.GetInt32();
        if (value < min || value > max)
            throw new ContentException($"{file}: {name} {value} is outside {min}..{max}");
        return value;
    }

    public static WaveDef[] LoadWaves(string json, VisitorDef[] visitors, string file)
    {
        using JsonDocument doc = Parse(json, file);
        JsonElement wavesEl = RequireProperty(doc.RootElement, "waves", file);

        // waveVariance: how much a wave's start offsets may be jittered, 0-100.
        // Composition, counts and spacing are untouched -- only WHEN each group
        // begins -- so the authored difficulty curve survives intact. See
        // balance-targets.md for the measured cost.
        int variance = 0;
        if (doc.RootElement.TryGetProperty("waveVariance", out JsonElement v))
        {
            variance = v.GetInt32();
            if (variance is < 0 or > 100)
                throw new ContentException($"{file}: waveVariance {variance} is outside 0..100");
        }

        // Wave pacing. All three default to the original behaviour -- no timer,
        // no premium, no bonus -- so an existing table plays exactly as before.
        int prepTicks = OptionalInt(doc.RootElement, "prepTicks", 0, 0, 3600, file);
        int midWavePercent = OptionalInt(doc.RootElement, "midWaveBuildPercent", 100, 100, 1000, file);
        int earlyCallGold = OptionalInt(doc.RootElement, "earlyCallGoldPerSecond", 0, 0, 100, file);
        int clearGold = OptionalInt(doc.RootElement, "waveClearGold", 0, 0, 2000, file);

        // One authored growth rate, compounded here rather than in the tick loop.
        // The balance targets want 1.10-1.18x wave to wave, so the content states
        // the rate and the loader turns it into a per-wave scalar.
        Fix32 growth = doc.RootElement.TryGetProperty("appetiteGrowth", out JsonElement g)
            ? ParseFix(g, file)
            : Fix32.One;

        if (growth < Fix32.One)
            throw new ContentException($"{file}: appetiteGrowth {growth} would make later waves weaker");

        // The wave the ramp starts from. Waves at or before it sit at scale 1.0,
        // so the opening is flat and the curve steepens afterwards.
        //
        // One knob could not do this. appetiteGrowth applies from wave 1, so the only
        // way to threaten wave 12 was a rate that also inflated waves 2-4 -- and
        // waves 2-4 are where the player is broke, so they were the binding
        // constraint on the whole curve. Six passes pushed that single scalar and
        // every one of them had to choose between a lethal opening and a trivial
        // ending. See 2026-08-07-early-economy-2-balance.md.
        //
        // Defaults to 1, which is exactly the previous behaviour: growth^(index-1).
        int growthFrom = doc.RootElement.TryGetProperty("appetiteGrowthFrom", out JsonElement gf)
            ? gf.GetInt32()
            : 1;

        if (growthFrom < 1)
            throw new ContentException($"{file}: appetiteGrowthFrom {growthFrom} must be at least 1");

        var waves = new List<WaveDef>();
        foreach (JsonElement w in wavesEl.EnumerateArray())
        {
            int index = RequireInt(w, "index", file);
            var entries = new List<WaveEntry>();
            foreach (JsonElement e in RequireProperty(w, "entries", file).EnumerateArray())
            {
                string visitorId = RequireString(e, "visitor", file);
                ushort visitorIndex = ushort.MaxValue;
                for (ushort k = 0; k < visitors.Length; k++)
                    if (visitors[k].Id == visitorId) { visitorIndex = k; break; }
                if (visitorIndex == ushort.MaxValue)
                    throw new ContentException($"{file}: wave {index} names unknown visitor '{visitorId}'");

                entries.Add(new WaveEntry
                {
                    VisitorIndex = visitorIndex,
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

            if (w.TryGetProperty("appetiteScale", out JsonElement explicitScale))
                scale = ParseFix(explicitScale, file);

            waves.Add(new WaveDef { Index = index, Entries = entries.ToArray(), AppetiteScale = scale, VariancePercent = variance, PrepTicks = prepTicks, MidWaveBuildPercent = midWavePercent, EarlyCallGoldPerSecond = earlyCallGold, ClearGold = clearGold });
        }

        waves.Sort((a, b) => a.Index.CompareTo(b.Index));
        // runWaves: play only the first N of the authored table.
        //
        // Truncation, not a re-tuning. The HP curve is authored per wave index,
        // so a shorter run is the SAME waves stopping earlier -- it is easier,
        // not merely shorter. balance-targets.md carries the measured cost.
        if (doc.RootElement.TryGetProperty("runWaves", out JsonElement runWaves))
        {
            int n = runWaves.GetInt32();
            if (n < 1 || n > waves.Count)
                throw new ContentException(
                    $"{file}: runWaves {n} is outside 1..{waves.Count} authored waves");
            waves.RemoveRange(n, waves.Count - n);
        }

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
            StartingPatience = r.TryGetProperty("startingPatience", out var sl) ? sl.GetInt32() : 20,
        };
        draft.Spawns.AddRange(spawns);

        // Absent means "every station", which is why this is not defaulted to the
        // full list here -- see MapDef.StationIds. An empty array in the file is
        // rejected rather than silently read as "all": a board that offers
        // nothing is a typo every time, and it would present as an empty toolbar
        // with no explanation.
        if (r.TryGetProperty("stations", out var tw))
        {
            if (tw.ValueKind != JsonValueKind.Array)
                throw new ContentException($"{file}: \"stations\" must be an array of station ids");

            foreach (JsonElement t in tw.EnumerateArray())
            {
                string stationId = t.GetString()
                    ?? throw new ContentException($"{file}: \"stations\" contains a non-string entry");
                if (draft.StationIds.Contains(stationId))
                    throw new ContentException($"{file}: \"stations\" lists '{stationId}' twice");
                draft.StationIds.Add(stationId);
            }

            if (draft.StationIds.Count == 0)
                throw new ContentException(
                    $"{file}: \"stations\" is empty -- omit the field for every station, "
                    + "or list the ones this board offers");
        }

        // Elevation: digit rows parallel to `cells`, one character per cell.
        //
        // Same shape as the cell rows on purpose -- an author edits them side by
        // side and a diff lines up. Absent means a flat board, which is what
        // every map written before elevation existed says by omission.
        if (r.TryGetProperty("heights", out var hs))
        {
            if (hs.ValueKind != JsonValueKind.Array)
                throw new ContentException($"{file}: \"heights\" must be an array of digit rows");

            var levels = new byte[width * height];
            int row = 0;
            foreach (JsonElement line in hs.EnumerateArray())
            {
                string text = line.GetString()
                    ?? throw new ContentException($"{file}: \"heights\" row {row} is not a string");
                if (row >= height)
                    throw new ContentException($"{file}: \"heights\" has more rows than the map is tall");
                if (text.Length != width)
                    throw new ContentException(
                        $"{file}: \"heights\" row {row} is {text.Length} long, expected {width}");

                for (int x = 0; x < width; x++)
                {
                    if (text[x] < '0' || text[x] > '9')
                        throw new ContentException(
                            $"{file}: \"heights\" row {row} has '{text[x]}', expected a digit 0-9");
                    levels[row * width + x] = (byte)(text[x] - '0');
                }
                row++;
            }
            if (row != height)
                throw new ContentException($"{file}: \"heights\" has {row} rows, expected {height}");
            draft.Heights = levels;
        }

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
