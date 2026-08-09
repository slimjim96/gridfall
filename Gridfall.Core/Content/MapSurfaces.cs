namespace Gridfall.Core.Content;

/// <summary>
/// The glyphs a surface layer is authored in, and the rule about which cell
/// kinds each one is legal on.
///
/// One place, because three callers need the same answer and they are in three
/// projects: the loader parses the layer, the board editor writes it, and
/// `MapValidator` judges it. The map format has been burned by this exact shape
/// of duplication before — the generator re-implemented the validator and
/// omitted a check, and three maps shipped warnings nobody could see
/// (2026-08-08).
/// </summary>
public static class MapSurfaces
{
    public const char GroundGlyph = '.';
    public const char WaterGlyph = '~';
    public const char SpanGlyph = '=';

    public static char ToGlyph(CellSurface surface) => surface switch
    {
        CellSurface.Water => WaterGlyph,
        CellSurface.Span => SpanGlyph,
        _ => GroundGlyph,
    };

    /// <summary>Parses a glyph, or null if it is not one.</summary>
    public static CellSurface? FromGlyph(char glyph) => glyph switch
    {
        GroundGlyph => CellSurface.Ground,
        WaterGlyph => CellSurface.Water,
        SpanGlyph => CellSurface.Span,
        _ => null,
    };

    /// <summary>Everything a visitor can stand on. Spawn and goal included.</summary>
    public static bool IsWalkable(CellKind kind)
        => kind is CellKind.PathOnly or CellKind.Buildable or CellKind.Spawn or CellKind.Goal;

    /// <summary>
    /// Whether this surface may be painted on this kind.
    ///
    /// **This is the rule that keeps surfaces view-only.** Water is legal only
    /// where the pathfinder already refuses to go, and a span only where it
    /// already goes — so a surface can never depict a rule the simulation is not
    /// following. Visitors do not walk on water because the cell was already
    /// Blocked, not because anything consulted the surface layer.
    ///
    /// Get this wrong and the failure is the worst kind: a board that looks like
    /// it has a river, plays like it does not, and validates either way.
    /// </summary>
    public static bool IsLegalOn(CellSurface surface, CellKind kind) => surface switch
    {
        CellSurface.Water => kind == CellKind.Blocked,
        CellSurface.Span => IsWalkable(kind),
        _ => true,
    };

    /// <summary>Why a combination was refused, for the finding text.</summary>
    public static string RefusalFor(CellSurface surface) => surface switch
    {
        CellSurface.Water => "water is only legal on a blocked cell -- "
                             + "painting it on a walkable one draws a river visitors stroll across",
        CellSurface.Span => "a span is only legal on a walkable cell -- "
                            + "a bridge nothing can cross is scenery, and should be blocked terrain",
        _ => "unknown surface",
    };
}
