using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;

namespace Gridfall.View;

/// <summary>One tile image, plus the average colour its raised sides are painted.</summary>
public readonly struct TerrainTile
{
    public required Texture2D Texture { get; init; }

    /// <summary>
    /// The image's mean colour, used for the side walls of a raised cell.
    ///
    /// Without it the sides come from the theme's colour ramp, and a tiles-only
    /// theme has no ramp -- so a stone wall on grass got slate-blue sides. The
    /// mean is computed once at load, not per rebuild.
    /// </summary>
    public required Color Average { get; init; }
}

/// <summary>
/// Terrain tiles read off disk: <c>presentation/tiles/[theme]/[kind]/[name].png</c>.
///
/// A folder IS a theme. Drop <c>presentation/tiles/rust/</c> in and `rust` becomes
/// selectable in the editor's F4 rotation and legal in a map's `theme` field --
/// no code change, no registration, no Godot reimport. That is deliberate: the
/// theme is already an opaque string that Core carries and never reads
/// (MapDef.Theme), so the view is free to decide what resolves it.
///
/// **Nothing here reaches the simulation.** A tile changes how a cell looks and
/// nothing else; CellKind still decides every rule. An image cannot make a cell
/// walkable, and there is no code path by which it could.
///
/// ## The folder contract
///
/// Kind folders are `buildable/`, `path/` (or `path-only/`), `blocked/`,
/// `spawn/`, `goal/`. Any of them may be absent -- a kind with no folder falls
/// back to the theme's flat colour, so a theme can be built one folder at a
/// time.
///
/// A file name is read as `[mask]-[variant].png`:
///
/// - **`[mask]`** is a connection mask when it is made only of the letters
///   `n` `e` `s` `w` (or the word `none`): `ns.png` is a straight, `es.png` a
///   corner, `nesw.png` a crossroads. The tile is chosen by which neighbours
///   connect, which is what makes a painted road join up.
/// - Anything else -- `grass.png`, `stone.png`, `bush.png` -- is an
///   **unmasked** variant, used for any cell of that kind.
/// - **`[variant]`** is everything after the first `-` and is ignored when
///   matching: `stone.png`, `stone-2.png` and `ns-cracked.png` are variants of
///   `stone`, `stone` and `ns`.
///
/// Beware that a name made only of compass letters is a mask even if you meant
/// a word: `sew.png` is the mask S|E|W, not a variant called "sew". Add a dash
/// suffix (`sew-1.png`) if you want it read as a variant.
///
/// Resolution order for a cell: exact mask, then unmasked variants, then the
/// theme colour. Multiple candidates are picked by cell coordinate, never at
/// random -- see <see cref="VariantIndex"/>.
/// </summary>
public static class TileLibrary
{
    /// <summary>Repo-relative, resolved against ContentFiles.FindRepoRoot().</summary>
    public const string Folder = "presentation/tiles";

    private static readonly Dictionary<string, ThemeTiles> Themes = new();
    private static string _root = "";

    public static IEnumerable<string> ThemeIds => Themes.Keys;

    /// <summary>
    /// Bumped by every <see cref="Scan"/>. A rescan builds brand-new ImageTextures,
    /// so anything caching meshes per texture must notice and drop them -- see
    /// WorldRenderer.CommitTileLayers, which otherwise accumulated a dead layer
    /// node per tile per F7.
    /// </summary>
    public static int Generation { get; private set; }

    public static bool Has(string? id) => id is not null && Themes.ContainsKey(id);

    public static ThemeTiles? For(string? id)
        => id is not null && Themes.TryGetValue(id, out ThemeTiles? theme) ? theme : null;

    /// <summary>
    /// (Re)read every theme folder. Safe to call again at runtime -- that is how
    /// the editor's "reload tiles" key works, and the reason you can drop a PNG
    /// in and see it without relaunching.
    /// </summary>
    public static void Scan(string repoRoot)
    {
        _root = Path.Combine(repoRoot, Folder);
        Themes.Clear();
        Generation++;

        if (!Directory.Exists(_root))
        {
            GD.Print($"tiles: no {Folder}/ -- every theme uses its colour ramp");
            return;
        }

        // Ordinal sort so two machines enumerate the same set in the same order.
        // Directory.GetDirectories order is filesystem-dependent and a theme
        // rotation that differs per machine is a bug report nobody can reproduce.
        foreach (string dir in Directory.GetDirectories(_root).OrderBy(d => d, System.StringComparer.Ordinal))
        {
            ThemeTiles? theme = ThemeTiles.Load(dir);
            if (theme is not null) Themes[theme.Id] = theme;
        }

        if (Themes.Count == 0)
        {
            GD.Print($"tiles: {Folder}/ has no usable theme folders");
            return;
        }

        GD.Print($"tiles: loaded {string.Join(", ", Themes.Select(t => $"{t.Key} ({t.Value.TileCount})"))}");

        // Named individually, not just counted. "loaded patchy (3)" reads as
        // success, and the board it produces does not look like one.
        foreach (KeyValuePair<string, ThemeTiles> theme in Themes)
        foreach (string gap in theme.Value.Gaps)
            GD.Print($"tiles: {theme.Key} -- {gap}");
    }

    // ---- connection masks -------------------------------------------------

    /// <summary>N=1, E=2, S=4, W=8 -- matching the tile file naming.</summary>
    public static int ConnectionMask(MapDef map, int x, int y)
    {
        CellKind self = map.Cells[map.Index(x, y)];
        int mask = 0;
        if (Connects(map, self, x, y - 1)) mask |= 1;
        if (Connects(map, self, x + 1, y)) mask |= 2;
        if (Connects(map, self, x, y + 1)) mask |= 4;
        if (Connects(map, self, x - 1, y)) mask |= 8;
        return mask;
    }

    private static bool Connects(MapDef map, CellKind self, int x, int y)
    {
        bool inside = x >= 0 && y >= 0 && x < map.Width && y < map.Height;

        // Off the board counts as Blocked, so a wall running along the map edge
        // draws as a continuing wall rather than as a row of dead ends.
        CellKind other = inside ? map.Cells[map.Index(x, y)] : CellKind.Blocked;
        return Group(self) == Group(other);
    }

    /// <summary>
    /// Which cells a tile considers "the same thing continuing".
    ///
    /// Spawn and goal join the road network: a corridor should visibly run into
    /// them rather than stopping one cell short. Buildable is open ground, so a
    /// road never bleeds into it -- if it did, every path tile beside open
    /// ground would draw as a junction and the road would dissolve.
    /// </summary>
    private static int Group(CellKind kind) => kind switch
    {
        CellKind.PathOnly or CellKind.Spawn or CellKind.Goal => 0,
        CellKind.Blocked => 1,
        _ => 2,
    };

    /// <summary>
    /// Which variant a cell gets: a fixed hash of its coordinates, never an RNG.
    ///
    /// The board is rebuilt on every brush stroke. Picking at random would
    /// reshuffle every tile on the map each time you painted one cell, which
    /// looks like a rendering bug and makes a screenshot unreproducible.
    /// </summary>
    public static int VariantIndex(int x, int y, int count)
    {
        if (count <= 1) return 0;

        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return (int)(h % (uint)count);
    }
}

/// <summary>Every tile of one theme, indexed by cell kind and connection mask.</summary>
public sealed class ThemeTiles
{
    private sealed class KindTiles
    {
        public readonly Dictionary<int, List<TerrainTile>> ByMask = new();
        public readonly List<TerrainTile> Unmasked = new();

        /// <summary>
        /// For each of the 16 masks, the mask actually drawn: itself when the
        /// tile exists, otherwise the nearest one that does, or -1 for none.
        ///
        /// Precomputed at load so resolving a cell is an array read, and so the
        /// substitution is decided once rather than per cell per rebuild.
        /// </summary>
        public readonly int[] Resolution = new int[16];

        /// <summary>How many masks are being substituted. 0 means the set is complete.</summary>
        public int Gaps;
    }

    private readonly Dictionary<CellKind, KindTiles> _kinds = new();

    public string Id { get; private init; } = "";
    public int TileCount { get; private set; }

    /// <summary>
    /// Human-readable notes about what this theme is missing, empty when it is
    /// complete.
    ///
    /// A partial tileset does not fail -- it substitutes -- and a substitution
    /// nobody is told about is the worst of both worlds. The first version fell
    /// straight through to the flat theme colour, so a theme with `ns` and `ew`
    /// but no corners drew a road with holes punched in it at every turn, and
    /// said "loaded patchy (3)" as though that were a success.
    /// </summary>
    public IReadOnlyList<string> Gaps => _gaps;

    private readonly List<string> _gaps = new();

    public bool IsComplete => _gaps.Count == 0;

    public int GapCount
    {
        get
        {
            int total = 0;
            foreach (KindTiles kind in _kinds.Values) total += kind.Gaps;
            return total;
        }
    }

    /// <summary>
    /// Folders that name a <see cref="CellKind"/> — one image per cell on the grid.
    /// </summary>
    private static readonly Dictionary<string, CellKind> KindFolders = new()
    {
        ["buildable"] = CellKind.Buildable,
        ["path"] = CellKind.PathOnly,
        ["path-only"] = CellKind.PathOnly,
        ["blocked"] = CellKind.Blocked,
        ["spawn"] = CellKind.Spawn,
        ["goal"] = CellKind.Goal,
    };

    /// <summary>
    /// The one folder that is NOT a cell kind: the surround the board sits in.
    ///
    /// Worth keeping the distinction sharp. Every other folder here answers
    /// "what does this cell look like" and is therefore downstream of a
    /// simulation concept. This one is scenery — it is not on the grid, nothing
    /// walks on it, and it can never be picked. If a second scene folder ever
    /// appears it belongs beside this one, not in the table above.
    /// </summary>
    private const string BackgroundFolder = "background";

    /// <summary>
    /// The tiling surround, or null when this theme has no `background/` folder —
    /// in which case the board keeps the empty scene colour behind it, exactly as
    /// it did before backgrounds existed.
    /// </summary>
    public TerrainTile? Background { get; private set; }

    public static ThemeTiles? Load(string directory)
    {
        var theme = new ThemeTiles { Id = Path.GetFileName(directory) };

        foreach (string kindDir in Directory.GetDirectories(directory).OrderBy(d => d, System.StringComparer.Ordinal))
        {
            string folder = Path.GetFileName(kindDir).ToLowerInvariant();

            if (folder == BackgroundFolder)
            {
                theme.LoadBackground(kindDir);
                continue;
            }

            if (!KindFolders.TryGetValue(folder, out CellKind kind))
            {
                GD.Print($"tiles: ignoring {theme.Id}/{folder}/ -- not a cell kind or 'background'");
                continue;
            }

            foreach (string file in Directory.GetFiles(kindDir, "*.png").OrderBy(f => f, System.StringComparer.Ordinal))
                theme.Add(kind, file);
        }

        if (theme.TileCount == 0) return null;

        theme.ResolveMasks();
        return theme;
    }

    /// <summary>
    /// Decide, once, which tile stands in for every mask this theme does not have.
    ///
    /// A kind with no masked tiles at all has not asked to auto-tile -- it is a
    /// pile of variants and that is a complete, deliberate tileset. Only a kind
    /// that uses SOME masks can have gaps.
    /// </summary>
    private void ResolveMasks()
    {
        foreach (KeyValuePair<CellKind, KindTiles> entry in _kinds)
        {
            KindTiles kind = entry.Value;

            for (int mask = 0; mask < 16; mask++)
                kind.Resolution[mask] = NearestMask(kind, mask);

            // Unmasked-only is a complete tileset -- it never asked to auto-tile.
            // And a kind with an unmasked variant alongside its masks has supplied
            // its own fallback deliberately, so it has no gaps either.
            if (kind.ByMask.Count == 0 || kind.Unmasked.Count > 0) continue;

            for (int mask = 0; mask < 16; mask++)
                if (kind.Resolution[mask] != mask) kind.Gaps++;

            if (kind.Gaps > 0)
                _gaps.Add($"{entry.Key}: {kind.Gaps} of 16 connection masks missing, substituted");
        }
    }

    /// <summary>
    /// The closest mask this theme actually has: fewest differing edges, ties
    /// broken by the lower mask so the choice is identical on every machine
    /// regardless of dictionary iteration order.
    ///
    /// Substituting a near-miss beats falling through to the flat theme colour.
    /// A corner drawn as a straight still reads as a road; a corner drawn as a
    /// hole reads as a rendering bug, and that is what the first version did.
    /// </summary>
    private static int NearestMask(KindTiles kind, int wanted)
    {
        if (kind.ByMask.TryGetValue(wanted, out List<TerrainTile>? exact) && exact.Count > 0)
            return wanted;

        int best = -1, bestScore = int.MaxValue;
        foreach (KeyValuePair<int, List<TerrainTile>> candidate in kind.ByMask)
        {
            if (candidate.Value.Count == 0) continue;

            int differing = System.Numerics.BitOperations.PopCount((uint)(candidate.Key ^ wanted));
            int score = differing * 16 + candidate.Key;
            if (score < bestScore) { bestScore = score; best = candidate.Key; }
        }
        return best;
    }

    /// <summary>
    /// The surround image: the first PNG in `background/`, ordinally.
    ///
    /// One, not a variant list. It is drawn on a single large quad with repeating
    /// UVs, so there is no per-cell choice to make — extra files would be dead
    /// weight nobody could tell was unused.
    /// </summary>
    private void LoadBackground(string directory)
    {
        string[] files = Directory.GetFiles(directory, "*.png");
        if (files.Length == 0) return;

        System.Array.Sort(files, System.StringComparer.Ordinal);

        Image? image = Image.LoadFromFile(files[0]);
        if (image is null)
        {
            GD.PrintErr($"tiles: could not read {files[0]}");
            return;
        }

        Background = new TerrainTile
        {
            Texture = ImageTexture.CreateFromImage(image),
            Average = MeanColour(image),
        };
        TileCount++;

        if (files.Length > 1)
            GD.Print($"tiles: {Id}/background/ has {files.Length} files; using {Path.GetFileName(files[0])}");
    }

    private void Add(CellKind kind, string file)
    {
        // Raw load, not GD.Load: these live outside res:// on purpose, so a new
        // PNG needs no Godot import step. "Drop it in and relaunch" was the
        // requirement; an import round trip is not that.
        Image? image = Image.LoadFromFile(file);
        if (image is null)
        {
            GD.PrintErr($"tiles: could not read {file}");
            return;
        }

        var tile = new TerrainTile
        {
            Texture = ImageTexture.CreateFromImage(image),
            Average = MeanColour(image),
        };

        if (!_kinds.TryGetValue(kind, out KindTiles? kindTiles))
            _kinds[kind] = kindTiles = new KindTiles();

        string stem = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
        if (TryParseMask(stem, out int mask))
        {
            if (!kindTiles.ByMask.TryGetValue(mask, out List<TerrainTile>? list))
                kindTiles.ByMask[mask] = list = new List<TerrainTile>();
            list.Add(tile);
        }
        else
        {
            kindTiles.Unmasked.Add(tile);
        }

        TileCount++;
    }

    /// <summary>`ns` / `esw` / `none` -> a mask. Anything else is an unmasked variant.</summary>
    private static bool TryParseMask(string stem, out int mask)
    {
        mask = 0;

        int dash = stem.IndexOf('-');
        string head = dash >= 0 ? stem[..dash] : stem;

        if (head == "none") return true;
        if (head.Length is 0 or > 4) return false;

        foreach (char c in head)
        {
            int bit = c switch { 'n' => 1, 'e' => 2, 's' => 4, 'w' => 8, _ => 0 };
            // A repeat means it is a word that happens to start with compass
            // letters ("nene" is not a mask), so it belongs in the variant pile.
            if (bit == 0 || (mask & bit) != 0) { mask = 0; return false; }
            mask |= bit;
        }
        return true;
    }

    public bool Has(CellKind kind) => _kinds.ContainsKey(kind);

    /// <summary>
    /// The tile for one cell, or false to fall back to the theme colour.
    ///
    /// Order: the exact mask, then the theme's own unmasked fallback, then the
    /// nearest mask it does have. Only a kind with no tiles at all returns false,
    /// so a theme can never draw part of a road and leave the rest as holes.
    /// </summary>
    public bool TryTile(CellKind kind, int mask, int x, int y, out TerrainTile tile)
    {
        tile = default;
        if (!_kinds.TryGetValue(kind, out KindTiles? kindTiles)) return false;

        if (kindTiles.ByMask.TryGetValue(mask, out List<TerrainTile>? exact) && exact.Count > 0)
        {
            tile = exact[TileLibrary.VariantIndex(x, y, exact.Count)];
            return true;
        }

        if (kindTiles.Unmasked.Count > 0)
        {
            tile = kindTiles.Unmasked[TileLibrary.VariantIndex(x, y, kindTiles.Unmasked.Count)];
            return true;
        }

        int substitute = kindTiles.Resolution[mask & 15];
        if (substitute >= 0)
        {
            List<TerrainTile> list = kindTiles.ByMask[substitute];
            tile = list[TileLibrary.VariantIndex(x, y, list.Count)];
            return true;
        }

        return false;
    }

    /// <summary>
    /// One tile standing for a kind, for the editor's brush swatch.
    ///
    /// Prefers the crossroads mask, then any mask, then an unmasked variant: a
    /// road brush showing `nesw` reads as "road" at swatch size, where a
    /// dead-end would read as a smudge.
    /// </summary>
    public Texture2D? Representative(CellKind kind)
    {
        if (!_kinds.TryGetValue(kind, out KindTiles? kindTiles)) return null;

        if (kindTiles.ByMask.TryGetValue(15, out List<TerrainTile>? cross) && cross.Count > 0)
            return cross[0].Texture;

        foreach (KeyValuePair<int, List<TerrainTile>> entry in kindTiles.ByMask.OrderByDescending(e => e.Key))
            if (entry.Value.Count > 0) return entry.Value[0].Texture;

        return kindTiles.Unmasked.Count > 0 ? kindTiles.Unmasked[0].Texture : null;
    }

    private static Color MeanColour(Image image)
    {
        // Sample on a stride rather than every pixel: the mean only paints side
        // walls, and a 64x64 read per tile per launch is work for a number that
        // is visually identical either way.
        const int stride = 4;
        float r = 0, g = 0, b = 0;
        int n = 0;

        for (int y = 0; y < image.GetHeight(); y += stride)
        for (int x = 0; x < image.GetWidth(); x += stride)
        {
            Color c = image.GetPixel(x, y);
            r += c.R; g += c.G; b += c.B;
            n++;
        }

        return n == 0 ? Colors.Gray : new Color(r / n, g / n, b / n);
    }
}
