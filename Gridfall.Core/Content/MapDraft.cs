using System.Text;

namespace Gridfall.Core.Content;

/// <summary>
/// A map being edited. `MapDef` is immutable and validated; this is the mutable
/// thing the board editor paints on, and it is allowed to be temporarily broken.
///
/// Serialisation patience here so the editor writes exactly the format the loader
/// reads -- one definition of the map format, not two (engine guide 07).
/// </summary>
public sealed class MapDraft
{
    public string Id = "untitled";

    /// <summary>
    /// Terrain palette id for the view. The simulation never reads it; see
    /// MapDef.Theme. Defaults to the palette the game shipped with, so an
    /// existing map that names no theme looks exactly as it always did.
    /// </summary>
    public string Theme = "slate";

    public int Width;
    public int Height;
    public CellKind[] Cells = Array.Empty<CellKind>();
    public int StartingGold = 200;
    public int StartingPatience = 20;

    /// <summary>
    /// Spawn ORDER is content, not layout: the block check and wave assignment
    /// iterate this, so reordering changes the game (engine guide 07).
    /// </summary>
    public readonly List<GridCell> Spawns = new();

    public GridCell Goal = GridCell.Invalid;

    /// <summary>
    /// Station ids this board offers, in toolbar order. Empty means all of them --
    /// see MapDef.StationIds. Carried through From/ToMapDef/ToJson so the editor
    /// cannot silently strip a roster off a map somebody opens and saves.
    /// </summary>
    public readonly List<string> StationIds = new();

    /// <summary>
    /// Per-cell elevation, row-major, 0-9. Empty means flat. View-only; see
    /// MapDef.Heights. Carried through From/ToMapDef/ToJson so the editor cannot
    /// silently flatten a board somebody opens and saves.
    /// </summary>
    public byte[] Heights = Array.Empty<byte>();

    /// <summary>
    /// Per-cell surface, row-major. Empty means all ground. View-only; see
    /// MapDef.Surfaces. Carried through From/ToMapDef/ToJson for the same reason
    /// Heights is: an editor that drops it drains every river on open-and-save.
    /// </summary>
    public byte[] Surfaces = Array.Empty<byte>();

    public int Index(int x, int y) => y * Width + x;
    public int Index(GridCell c) => c.Y * Width + c.X;
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public bool InBounds(GridCell c) => InBounds(c.X, c.Y);

    public static MapDraft Blank(int width, int height, string id = "untitled")
    {
        var draft = new MapDraft { Id = id, Width = width, Height = height };
        draft.Cells = new CellKind[width * height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            draft.Cells[draft.Index(x, y)] =
                (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    ? CellKind.Blocked
                    : CellKind.Buildable;

        // A blank map that fails validation on creation is a bad first
        // experience, so seed a legal spawn and goal on the middle row.
        int mid = height / 2;
        draft.Cells[draft.Index(0, mid)] = CellKind.Spawn;
        draft.Spawns.Add(new GridCell(0, mid));
        draft.Cells[draft.Index(width - 1, mid)] = CellKind.Goal;
        draft.Goal = new GridCell(width - 1, mid);
        for (int x = 1; x < width - 1; x++) draft.Cells[draft.Index(x, mid)] = CellKind.Buildable;

        return draft;
    }

    public static MapDraft From(MapDef map)
    {
        var draft = new MapDraft
        {
            Id = map.Id,
            Theme = map.Theme,
            Width = map.Width,
            Height = map.Height,
            Cells = (CellKind[])map.Cells.Clone(),
            StartingGold = map.StartingGold,
            StartingPatience = map.StartingPatience,
            Goal = map.Goal,
        };
        draft.Spawns.AddRange(map.Spawns);
        draft.StationIds.AddRange(map.StationIds);
        draft.Heights = (byte[])map.Heights.Clone();
        draft.Surfaces = (byte[])map.Surfaces.Clone();
        return draft;
    }

    /// <summary>
    /// Builds the immutable form. Does NOT validate -- callers run
    /// <see cref="MapValidator"/> first and decide what to do about findings.
    /// </summary>
    public MapDef ToMapDef() => new()
    {
        Id = Id,
        Theme = Theme,
        Width = Width,
        Height = Height,
        Cells = (CellKind[])Cells.Clone(),
        Spawns = Spawns.ToArray(),
        Goal = Goal,
        StartingGold = StartingGold,
        StartingPatience = StartingPatience,
        StationIds = StationIds.ToArray(),
        Heights = (byte[])Heights.Clone(),
        Surfaces = (byte[])Surfaces.Clone(),
    };

    /// <summary>
    /// Paints a cell, keeping the spawn list and goal consistent with the glyphs.
    ///
    /// Doing this in one place is why the editor cannot produce a map whose
    /// `spawns` array disagrees with its `S` cells -- a failure mode the loader
    /// rejects and a hand-editor hits constantly.
    /// </summary>
    /// <summary>
    /// Raise or lower a cell's elevation, clamped to 0-9.
    ///
    /// Allocates the height field on first use so a board that is never
    /// sculpted keeps writing no `heights` at all -- flat has to stay the
    /// absence of the field, not a field of zeroes.
    /// </summary>
    public void Raise(GridCell cell, int delta)
    {
        if (!InBounds(cell)) return;
        if (Heights.Length != Width * Height) Heights = new byte[Width * Height];

        int level = Heights[Index(cell)] + delta;
        Heights[Index(cell)] = (byte)(level < 0 ? 0 : level > 9 ? 9 : level);
    }

    /// <summary>Elevation level of a cell, 0 on a flat board.</summary>
    public int HeightAt(GridCell cell)
        => Heights.Length == Width * Height && InBounds(cell) ? Heights[Index(cell)] : 0;

    /// <summary>
    /// Paint a surface. Allocates on first use, so a board with no water keeps
    /// writing no `surfaces` field at all — plain ground has to stay the absence
    /// of the layer, not a layer of dots.
    /// </summary>
    public void PaintSurface(GridCell cell, CellSurface surface)
    {
        if (!InBounds(cell)) return;
        if (Surfaces.Length != Width * Height) Surfaces = new byte[Width * Height];
        Surfaces[Index(cell)] = (byte)surface;
    }

    /// <summary>Surface of a cell, Ground on a board that declares none.</summary>
    public CellSurface SurfaceAt(GridCell cell)
        => Surfaces.Length == Width * Height && InBounds(cell)
            ? (CellSurface)Surfaces[Index(cell)]
            : CellSurface.Ground;

    public void Paint(GridCell cell, CellKind kind)
    {
        if (!InBounds(cell)) return;
        int index = Index(cell);
        CellKind previous = Cells[index];
        if (previous == kind) return;

        if (previous == CellKind.Spawn) Spawns.RemoveAll(s => s == cell);
        if (previous == CellKind.Goal) Goal = GridCell.Invalid;

        if (kind == CellKind.Goal)
        {
            // Exactly one goal: placing a new one moves the old.
            if (Goal.IsValid) Cells[Index(Goal)] = CellKind.Buildable;
            Goal = cell;
        }
        if (kind == CellKind.Spawn && !Spawns.Contains(cell)) Spawns.Add(cell);

        Cells[index] = kind;
    }

    public void Resize(int width, int height)
    {
        var grown = new CellKind[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            grown[y * width + x] = InBounds(x, y)
                ? Cells[Index(x, y)]                       // anchored to the north corner
                : (x == 0 || y == 0 || x == width - 1 || y == height - 1
                    ? CellKind.Blocked
                    : CellKind.Buildable);

        Width = width;
        Height = height;
        Cells = grown;

        Spawns.RemoveAll(s => !InBounds(s));
        if (Goal.IsValid && !InBounds(Goal)) Goal = GridCell.Invalid;
    }

    public MapDraft Clone()
    {
        var copy = From(ToMapDef());
        copy.Id = Id;
        return copy;
    }

    /// <summary>
    /// The rows-of-strings format from engine guide 07. Rows are strings on
    /// purpose: a map diffs readably in git and a human can see its shape in a
    /// pull request.
    /// </summary>
    /// <summary>
    /// Serialise for `content-data/maps/`.
    ///
    /// Every line ends in an explicit "\n", never <c>AppendLine</c>: that emits
    /// <see cref="System.Environment.NewLine"/>, so the board editor would write
    /// CRLF on Windows and LF everywhere else. The map would still load — but a
    /// map saved on one machine and opened on another shows as a whole-file diff,
    /// and the generated maps are byte-compared to prove the generator is
    /// idempotent. Content files are data, and data does not get a platform.
    /// </summary>
    private const string Nl = "\n";

    public string ToJson()
    {
        var sb = new StringBuilder();
        sb.Append("{").Append(Nl);
        sb.Append($"  \"id\": \"{Id}\",").Append(Nl);
        sb.Append($"  \"theme\": \"{Theme}\",").Append(Nl);
        sb.Append("  \"version\": 1,").Append(Nl);
        sb.Append($"  \"width\": {Width},").Append(Nl);
        sb.Append($"  \"height\": {Height},").Append(Nl);
        sb.Append("  \"cells\": [").Append(Nl);

        for (int y = 0; y < Height; y++)
        {
            var row = new StringBuilder(Width);
            for (int x = 0; x < Width; x++)
            {
                row.Append(Cells[Index(x, y)] switch
                {
                    CellKind.PathOnly => '.',
                    CellKind.Buildable => 'b',
                    CellKind.Blocked => '#',
                    CellKind.Spawn => 'S',
                    CellKind.Goal => 'G',
                    _ => 'b',
                });
            }
            sb.Append($"    \"{row}\"{(y == Height - 1 ? "" : ",")}").Append(Nl);
        }

        sb.Append("  ],").Append(Nl);
        sb.Append("  \"spawns\": [");
        for (int i = 0; i < Spawns.Count; i++)
            sb.Append($"{(i == 0 ? "" : ", ")}{{ \"x\": {Spawns[i].X}, \"y\": {Spawns[i].Y} }}");
        sb.Append("],").Append(Nl);
        sb.Append($"  \"goal\": {{ \"x\": {Goal.X}, \"y\": {Goal.Y} }},").Append(Nl);
        sb.Append($"  \"startingGold\": {StartingGold},").Append(Nl);
        sb.Append($"  \"startingPatience\": {StartingPatience},").Append(Nl);
        // Omitted when flat, so a board with no hills reads exactly as it did
        // before elevation existed.
        if (Heights.Length == Width * Height && System.Array.Exists(Heights, h => h != 0))
        {
            sb.Append("  \"heights\": [").Append(Nl);
            for (int y = 0; y < Height; y++)
            {
                var row = new StringBuilder(Width);
                for (int x = 0; x < Width; x++) row.Append((char)('0' + Heights[Index(x, y)]));
                sb.Append($"    \"{row}\"{(y == Height - 1 ? "" : ",")}").Append(Nl);
            }
            sb.Append("  ],").Append(Nl);
        }

        // Same omission rule as heights: a board with no water writes no layer.
        if (Surfaces.Length == Width * Height && System.Array.Exists(Surfaces, s => s != 0))
        {
            sb.Append("  \"surfaces\": [").Append(Nl);
            for (int y = 0; y < Height; y++)
            {
                var row = new StringBuilder(Width);
                for (int x = 0; x < Width; x++)
                    row.Append(MapSurfaces.ToGlyph((CellSurface)Surfaces[Index(x, y)]));
                sb.Append($"    \"{row}\"{(y == Height - 1 ? "" : ",")}").Append(Nl);
            }
            sb.Append("  ],").Append(Nl);
        }

        // Omitted entirely when empty, because "" and "every station" are different
        // statements -- writing `"stations": []` would turn "all of them" into "none
        // of them" the next time this file is read.
        if (StationIds.Count > 0)
            sb.Append($"  \"stations\": [{string.Join(", ", StationIds.Select(t => $"\"{t}\""))}],").Append(Nl);
        sb.Append("  \"meta\": { \"author\": \"board-editor\" }").Append(Nl);
        sb.Append("}").Append(Nl);
        return sb.ToString();
    }
}
