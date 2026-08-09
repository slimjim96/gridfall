using Gridfall.Core.Math;

namespace Gridfall.Core;

/// <summary>Integer grid coordinate. The sim knows only these (engine guide 07).</summary>
public readonly struct GridCell : IEquatable<GridCell>
{
    public readonly int X;
    public readonly int Y;

    public GridCell(int x, int y) { X = x; Y = y; }

    public static readonly GridCell Invalid = new(-1, -1);

    public bool IsValid => X >= 0 && Y >= 0;

    public bool Equals(GridCell other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is GridCell c && Equals(c);
    public override int GetHashCode() => (X << 16) ^ Y;
    public static bool operator ==(GridCell a, GridCell b) => a.Equals(b);
    public static bool operator !=(GridCell a, GridCell b) => !a.Equals(b);
    public override string ToString() => $"({X},{Y})";
}

/// <summary>What a cell is. One byte, stored in the map's cell array.</summary>
public enum CellKind : byte
{
    /// <summary>Visitors walk it, you cannot build on it.</summary>
    PathOnly = 0,
    /// <summary>Visitors walk it until you build on it.</summary>
    Buildable = 1,
    /// <summary>Permanent scenery. Never walkable, never buildable.</summary>
    Blocked = 2,
    Spawn = 3,
    Goal = 4,
}

/// <summary>
/// What a cell is made of, as opposed to what it does.
///
/// **Presentation only.** The simulation never reads this — same standing as
/// <c>MapDef.Theme</c> and <c>MapDef.Heights</c>. A river is a <see
/// cref="CellKind.Blocked"/> cell that is drawn as water; a span is a walkable
/// cell that is drawn as a bridge deck. Geometry does not move, so a board with
/// a river through it plays exactly as the same board without one, and adding
/// surfaces re-records no traces.
///
/// That is the whole safety argument, and it is enforced rather than promised:
/// <c>MapValidator</c> refuses water on a walkable cell and a span on an
/// unwalkable one, so a surface can never imply a rule the pathfinder is not
/// following. Visitors do not walk on water because the cell was already
/// blocked — not because anything checked the surface.
/// </summary>
public enum CellSurface : byte
{
    /// <summary>Ordinary terrain. The default, and what every map says by omission.</summary>
    Ground = 0,
    /// <summary>Water. Only legal on a Blocked cell.</summary>
    Water = 1,
    /// <summary>A bridge, causeway or boardwalk deck. Only legal on a walkable cell.</summary>
    Span = 2,
}

/// <summary>
/// The four directions, in the order the flow field visits them.
/// THIS ORDER IS LOAD-BEARING: it decides which of two equal-cost routes wins.
/// Changing it changes the game. See engine guide 06.
/// </summary>
public static class Directions
{
    public const byte North = 0;
    public const byte East = 1;
    public const byte South = 2;
    public const byte West = 3;

    /// <summary>N, E, S, W. Do not reorder.</summary>
    public static readonly (int dx, int dy)[] Offsets =
    {
        (0, -1), // North
        (1, 0),  // East
        (0, 1),  // South
        (-1, 0), // West
    };

    public static byte Opposite(byte dir) => (byte)((dir + 2) & 3);

    public static FixVec2 ToVector(byte dir) => dir switch
    {
        North => new FixVec2(Fix32.Zero, -Fix32.One),
        East => new FixVec2(Fix32.One, Fix32.Zero),
        South => new FixVec2(Fix32.Zero, Fix32.One),
        _ => new FixVec2(-Fix32.One, Fix32.Zero),
    };
}
