using System.Collections.Generic;
using Godot;

namespace Gridfall.View;

/// <summary>
/// A map's ground palette. Themes exist so boards read as places -- forest,
/// desert, ocean -- without changing a single rule.
///
/// **A theme is three colours, not five.** Spawn and goal keep the same hue on
/// every board, because they are functional markers rather than terrain: a
/// player learns "purple is where they come from, green is what I am defending"
/// once, and a theme that moved them would make that knowledge worthless. Same
/// reasoning as the one-red rule in art-direction.md.
///
/// Every theme obeys the two palette constraints or it is wrong:
///
/// 1. **Terrain never competes with a unit.** Towers are the only warm saturated
///    things on the board. `desert` is the stress case -- it is an ochre, not an
///    orange, and it sits well under the tower slot in both saturation and
///    lightness. A sand-coloured board that reads as warm as a tower has broken
///    the rule even if it looks nice in isolation.
/// 2. **Buildable is terrain plus a subtle lift; path-only is terrain, darker.**
///    The three tiers must separate at default zoom without a legend.
///
/// Judge both from a rendered capture, never from the hex. ACES tonemapping plus
/// ambient light compresses the dark end hard, and the original slate ramp was
/// separated only after a real frame showed three values that looked distinct as
/// hex arriving as one flat grey.
/// </summary>
public readonly struct TerrainTheme
{
    public required Color Blocked { get; init; }
    public required Color PathOnly { get; init; }
    public required Color Buildable { get; init; }

    /// <summary>The palette the game shipped with, and the fallback.</summary>
    public const string Default = "slate";

    private static readonly Dictionary<string, TerrainTheme> Themes = new()
    {
        // Cool blue-grey. The original, kept as the default so every existing map
        // looks exactly as it did before themes existed.
        ["slate"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("1b2229"),
            PathOnly = Color.FromHtml("323f4d"),
            Buildable = Color.FromHtml("55697d"),
        },

        // Damp undergrowth. Green pulled well down in saturation so foliage never
        // reads as a unit.
        ["forest"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("17211a"),
            PathOnly = Color.FromHtml("2c3b2b"),
            Buildable = Color.FromHtml("4c6247"),
        },

        // The hard one, and the reason constraint 3 exists.
        //
        // The first attempt was a sand ochre (6b5c45). It cleared the tower slot
        // comfortably and still failed: it landed in the BRUTE's hue family
        // (b8a05a), and a capture showed khaki cubes nearly camouflaged on it.
        // Terrain competing with a creep is the same failure as competing with a
        // tower, and nothing in art-direction.md said so until now.
        //
        // Clay instead of sand -- rotated toward red, away from the khaki/husk
        // band, and kept dark. Desert is the tightest theme in the set because the
        // roster already owns most of the warm spectrum; that is exactly why the
        // original palette rule says terrain is cool.
        ["desert"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("231710"),
            PathOnly = Color.FromHtml("402a1e"),
            Buildable = Color.FromHtml("6b4a37"),
        },

        // Open water seen from above: blue, cold, high contrast between tiers.
        ["ocean"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("101d26"),
            PathOnly = Color.FromHtml("22404f"),
            Buildable = Color.FromHtml("3c6d81"),
        },

        // Below the surface. The first attempt separated this from `ocean` by hue
        // -- teal instead of blue -- and a capture killed it twice over: the mint
        // and teal creeps lost contrast, and the GOAL marker (46a07a) all but
        // disappeared into the ground. Losing a functional marker is worse than
        // losing a creep, and neither is acceptable.
        //
        // Separated by VALUE instead: this is ocean's hue taken much darker and a
        // touch violet. Depth reading as less light rather than more green is also
        // the physically honest version.
        ["underwater"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("0a1220"),
            PathOnly = Color.FromHtml("152539"),
            Buildable = Color.FromHtml("27415f"),
        },

        // Cold stone. Violet-grey, the least saturated theme in the set.
        ["mountain"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("1a181e"),
            PathOnly = Color.FromHtml("373443"),
            Buildable = Color.FromHtml("5b5668"),
        },

        // Hull plating against a void. The darkest ramp -- and the one to re-check
        // first if the board background colour ever changes, since it has the least
        // room between blocked terrain and the empty scene behind it.
        // Pale cold ground. The LIGHTEST ramp in the set, and the one to re-check
        // if a creep is ever added above the runner's mint in value -- a light
        // terrain is where a light unit disappears.
        ["tundra"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("2b333a"),
            PathOnly = Color.FromHtml("4a5763"),
            Buildable = Color.FromHtml("6e7f8c"),
        },

        // Neutral dark grey. Deliberately the only ramp with no hue at all, so it
        // is the safe backdrop: nothing in the roster can collide with a colour
        // that is not there.
        ["ash"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("17181a"),
            PathOnly = Color.FromHtml("2f3134"),
            Buildable = Color.FromHtml("4c4f53"),
        },

        // Standing water over peat. Teal rather than green so it separates from
        // `forest`, and darker than `ocean` so it separates from that too --
        // the two neighbours it would otherwise be confused with.
        ["marsh"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("141f1e"),
            PathOnly = Color.FromHtml("263a38"),
            Buildable = Color.FromHtml("3f5a58"),
        },

        ["space"] = new TerrainTheme
        {
            Blocked = Color.FromHtml("0f0f18"),
            PathOnly = Color.FromHtml("262338"),
            Buildable = Color.FromHtml("454162"),
        },
    };

    /// <summary>The built-in colour ramps, above.</summary>
    public static IEnumerable<string> Ids => Themes.Keys;

    /// <summary>
    /// Every selectable theme: the colour ramps plus every folder discovered
    /// under presentation/tiles/. A theme may be one, the other, or both --
    /// a ramp with a matching tile folder uses the tiles and keeps the ramp as
    /// the fallback for any cell kind the folder does not cover.
    ///
    /// Sorted so the editor's F4 rotation is the same order on every machine.
    /// </summary>
    public static IEnumerable<string> AllIds
    {
        get
        {
            var ids = new SortedSet<string>(Themes.Keys, System.StringComparer.Ordinal);
            foreach (string id in TileLibrary.ThemeIds) ids.Add(id);
            return ids;
        }
    }

    /// <summary>
    /// The named theme, or the default if it is not registered.
    ///
    /// Falls back rather than throwing: Core carries the theme as an opaque
    /// string and holds no list of valid ones, so a typo reaches here. A board
    /// drawn in the wrong palette is a far better failure than a map that will
    /// not open -- and `EveryShippedMapNamesAKnownTheme` catches the typo in CI.
    /// </summary>
    public static TerrainTheme For(string? id)
        => id is not null && Themes.TryGetValue(id, out TerrainTheme theme)
            ? theme
            : Themes[Default];

    /// <summary>A theme exists if it has a colour ramp OR a tile folder.</summary>
    public static bool IsKnown(string? id)
        => id is not null && (Themes.ContainsKey(id) || TileLibrary.Has(id));

    public Color ColourFor(Gridfall.Core.CellKind kind) => kind switch
    {
        Gridfall.Core.CellKind.Buildable => Buildable,
        Gridfall.Core.CellKind.PathOnly => PathOnly,
        Gridfall.Core.CellKind.Blocked => Blocked,
        // Functional markers, identical on every board -- see the class remarks.
        Gridfall.Core.CellKind.Spawn => Palette.TerrainSpawn,
        Gridfall.Core.CellKind.Goal => Palette.TerrainGoal,
        _ => Buildable,
    };
}
