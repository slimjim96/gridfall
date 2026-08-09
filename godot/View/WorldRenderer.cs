using System.Collections.Generic;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Path;
using Gridfall.View.Placeholders;

namespace Gridfall.View;

/// <summary>
/// The board. One ArrayMesh for the whole grid, rebuilt only when the walkable
/// grid actually changes -- not one node per cell, which at 64x64 would be
/// 4,096 nodes for something static.
/// </summary>
public sealed partial class WorldRenderer : Node3D
{
    private readonly MeshInstance3D _terrain = new();

    // Resolved from the map on every Initialise, so the editor picks up a theme
    // change by rebuilding rather than by anyone remembering to set it.
    private TerrainTheme _theme = TerrainTheme.For(null);
    /// <summary>One mesh per tile image, kept between rebuilds. See CommitTileLayers.</summary>
    private readonly Dictionary<Texture2D, MeshInstance3D> _tileLayers = new();
    private int _tileGeneration = -1;

    private readonly MeshInstance3D _hover = new();
    private readonly MeshInstance3D _errors = new();
    private readonly MeshInstance3D _range = new();
    private MapDef _map = null!;
    private PathSystem? _path;
    private ushort _builtForVersion = ushort.MaxValue;

    public override void _Ready()
    {
        AddChild(_terrain);

        _hover.Mesh = Shapes.GroundQuad(IsoGrid.CellSize * 0.94f);
        _hover.MaterialOverride = Palette.Matte(Palette.BuildPreviewOk, unshaded: true);
        _hover.Visible = false;
        AddChild(_hover);

        _errors.Visible = false;
        AddChild(_errors);

        _range.Visible = false;
        AddChild(_range);
    }

    public void Initialise(MapDef map, PathSystem path)
    {
        _map = map;
        _path = path;
        _theme = TerrainTheme.For(map.Theme);
        _builtForVersion = ushort.MaxValue;   // force a rebuild: this is a new field
        Rebuild();
    }

    /// <summary>
    /// Draw the board with no flow field. The editor's draft is often illegal
    /// mid-stroke -- no goal yet, a stranded spawn -- and refusing to draw until
    /// it is legal would make the editor unusable exactly when you need to see
    /// what you are doing.
    /// </summary>
    public void InitialiseGeometryOnly(MapDef map)
    {
        _map = map;
        _path = null;
        _theme = TerrainTheme.For(map.Theme);
        _builtForVersion = ushort.MaxValue;
        Rebuild();
    }

    /// <summary>Outline the cells the validator objected to.</summary>
    public void SetErrorCells(System.Collections.Generic.List<GridCell> cells)
    {
        if (cells.Count == 0) { _errors.Visible = false; return; }

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (GridCell cell in cells)
        {
            Vector3 c = IsoGrid.CellCentre(cell.X, cell.Y, IsoGrid.DecalHeight + 0.006f);
            const float h = IsoGrid.CellSize * 0.46f;
            Color colour = Palette.Danger.SrgbToLinear();
            surface.SetColor(colour);
            surface.AddVertex(new Vector3(c.X - h, c.Y, c.Z - h));
            surface.AddVertex(new Vector3(c.X + h, c.Y, c.Z - h));
            surface.AddVertex(new Vector3(c.X + h, c.Y, c.Z + h));
            surface.AddVertex(new Vector3(c.X - h, c.Y, c.Z - h));
            surface.AddVertex(new Vector3(c.X + h, c.Y, c.Z + h));
            surface.AddVertex(new Vector3(c.X - h, c.Y, c.Z + h));
        }
        surface.GenerateNormals();
        _errors.Mesh = surface.Commit();

        StandardMaterial3D material = Palette.Matte(Colors.White, unshaded: true);
        material.VertexColorUseAsAlbedo = true;
        _errors.MaterialOverride = material;
        _errors.Visible = true;
    }

    /// <summary>Cheap to call every frame: it returns immediately unless the grid moved.</summary>
    public void RebuildIfChanged()
    {
        if (_path is null || _path.Version == _builtForVersion) return;
        Rebuild();
    }

    public void ShowHover(GridCell cell, bool legal)
    {
        _hover.Visible = true;
        _hover.Position = IsoGrid.CellCentre(cell.X, cell.Y, IsoGrid.DecalHeight);
        ((StandardMaterial3D)_hover.MaterialOverride).AlbedoColor =
            legal ? Palette.BuildPreviewOk : Palette.Danger;
    }

    public void HideHover()
    {
        _hover.Visible = false;
        _range.Visible = false;
    }

    /// <summary>
    /// The reach of the station about to be placed, as a ring on the ground.
    ///
    /// A ring rather than a filled disc: at the ranges these stations have it
    /// would cover a third of the board, and the board is the thing the player
    /// is reading. The outline answers "does this cover the corner?" without
    /// hiding the corner.
    ///
    /// Drawn from the cell centre. TargetingSystem compares grid coordinates
    /// (station at `(cellX, cellY)`, visitor at its FixVec2), and the renderer adds
    /// the same half-cell to both — so the relative geometry is preserved and a
    /// ring centred on the cell is the honest picture of the comparison.
    /// </summary>
    public void ShowRange(GridCell cell, float radiusCells, bool legal)
    {
        const int Segments = 72;
        const float Thickness = 0.05f;

        float radius = radiusCells * IsoGrid.CellSize;
        Vector3 centre = IsoGrid.CellCentre(cell.X, cell.Y, IsoGrid.DecalHeight + 0.004f);
        Color colour = (legal ? Palette.BuildPreviewOk : Palette.Danger).SrgbToLinear();
        colour.A = 0.85f;

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < Segments; i++)
        {
            float a0 = Mathf.Tau * i / Segments;
            float a1 = Mathf.Tau * (i + 1) / Segments;

            Vector3 InnerOuter(float angle, float r) => new(
                centre.X + Mathf.Cos(angle) * r, centre.Y, centre.Z + Mathf.Sin(angle) * r);

            Vector3 i0 = InnerOuter(a0, radius - Thickness), o0 = InnerOuter(a0, radius + Thickness);
            Vector3 i1 = InnerOuter(a1, radius - Thickness), o1 = InnerOuter(a1, radius + Thickness);

            surface.SetColor(colour); surface.AddVertex(i0);
            surface.SetColor(colour); surface.AddVertex(o0);
            surface.SetColor(colour); surface.AddVertex(o1);

            surface.SetColor(colour); surface.AddVertex(i0);
            surface.SetColor(colour); surface.AddVertex(o1);
            surface.SetColor(colour); surface.AddVertex(i1);
        }

        surface.GenerateNormals();
        _range.Mesh = surface.Commit();

        StandardMaterial3D material = Palette.Matte(Colors.White, unshaded: true);
        material.VertexColorUseAsAlbedo = true;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _range.MaterialOverride = material;
        _range.Visible = true;
    }

    private void Rebuild()
    {
        ThemeTiles? tiles = TileLibrary.For(_map.Theme);

        // Untextured cell tops AND every side wall, in one vertex-coloured mesh.
        var flat = new SurfaceTool();
        flat.Begin(Mesh.PrimitiveType.Triangles);

        // One mesh per distinct tile image. A themed board uses a couple of dozen
        // at most, so this is a couple of dozen draw calls for the whole grid --
        // still nothing like the 4,096 nodes a node-per-cell board would cost.
        var textured = new Dictionary<Texture2D, SurfaceTool>();

        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                int index = _map.Index(x, y);
                CellKind kind = _map.Cells[index];

                // A station occupies the cell: raise it so the maze is legible as
                // shape, not only as colour. Blocked terrain is raised further.
                //
                // The kind test is load-bearing: PathSystem.IsBlocked is true for
                // blocked TERRAIN as well as for a station, so without it every
                // wall counted as occupied and lost its tile -- a stone theme
                // rendered its walls in flat ramp colour and looked untextured.
                bool occupied = kind != CellKind.Blocked && _path is not null && _path.IsBlocked(index);
                float height = kind == CellKind.Blocked ? 0.28f : occupied ? 0.10f : 0.0f;

                Color colour = _theme.ColourFor(kind);

                // An occupied cell keeps its tile. The raise and the station on top
                // of it are what say "occupied" -- swapping in the ramp colour
                // instead just put a slate pad on a grass board.
                if (tiles is not null &&
                    tiles.TryTile(kind, TileLibrary.ConnectionMask(_map, x, y), x, y, out TerrainTile tile))
                {
                    if (!textured.TryGetValue(tile.Texture, out SurfaceTool? surface))
                    {
                        textured[tile.Texture] = surface = new SurfaceTool();
                        surface.Begin(Mesh.PrimitiveType.Triangles);
                    }

                    AddTop(surface, x, y, height, colour: null);
                    colour = tile.Average;   // so the sides match the tile, not the ramp
                }
                else
                {
                    AddTop(flat, x, y, height, colour);
                }

                if (height > 0f) AddSides(flat, x, y, height, colour);
            }
        }

        flat.GenerateNormals();
        _terrain.Mesh = flat.Commit();

        // Unshaded on purpose. With lighting applied, ACES tonemapping plus ambient
        // compressed every terrain tone toward white and the palette stopped
        // meaning anything -- two separate colour passes changed nothing visible.
        // Unshaded makes the board render exactly the values art-direction.md
        // names, which is what a board you reason about should do. Units stay
        // shaded, so they read as objects sitting on it.
        StandardMaterial3D material = Palette.Matte(Colors.White, unshaded: true);
        material.VertexColorUseAsAlbedo = true;   // typed, not Set("...") by string
        _terrain.MaterialOverride = material;

        CommitTileLayers(textured);

        _builtForVersion = _path?.Version ?? ushort.MaxValue;
    }

    /// <summary>
    /// Hand each texture's mesh to a persistent layer node.
    ///
    /// Reused rather than recreated: the board rebuilds on every brush stroke,
    /// and freeing and re-adding twenty MeshInstance3Ds per stroke is churn for
    /// no gain. Layers whose texture is no longer on the board are hidden, so a
    /// theme change leaves no stale geometry behind.
    /// </summary>
    private void CommitTileLayers(Dictionary<Texture2D, SurfaceTool> textured)
    {
        // A rescan (the editor's F7) builds all-new ImageTextures, so every cached
        // layer is keyed on a texture nothing will ask for again. Hiding them
        // leaked a node per tile per reload; drop them instead.
        if (_tileGeneration != TileLibrary.Generation)
        {
            _tileGeneration = TileLibrary.Generation;
            foreach (MeshInstance3D stale in _tileLayers.Values)
            {
                RemoveChild(stale);
                stale.QueueFree();
            }
            _tileLayers.Clear();
        }

        foreach (KeyValuePair<Texture2D, SurfaceTool> entry in textured)
        {
            entry.Value.GenerateNormals();

            if (!_tileLayers.TryGetValue(entry.Key, out MeshInstance3D? layer))
            {
                _tileLayers[entry.Key] = layer = new MeshInstance3D();

                StandardMaterial3D material = Palette.Matte(Colors.White, unshaded: true);
                material.AlbedoTexture = entry.Key;
                // Nearest, because placeholder tiles are small and blocky and
                // linear filtering turns a 64px road edge into mush at this zoom.
                material.TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps;
                layer.MaterialOverride = material;

                AddChild(layer);
            }

            layer.Mesh = entry.Value.Commit();
            layer.Visible = true;
        }

        foreach (KeyValuePair<Texture2D, MeshInstance3D> entry in _tileLayers)
            if (!textured.ContainsKey(entry.Key)) entry.Value.Visible = false;
    }

    /// <summary>
    /// The top face of a cell. Pass a colour to tint it, or null to leave the
    /// vertices white so a texture shows through unmodified.
    ///
    /// UVs put (0,0) at the cell's north-west corner and V increasing south, so
    /// a tile image is "the board seen from above with north at the top". The
    /// camera's 45-degree yaw then turns the image's north edge into the
    /// up-and-right screen edge -- which is what a road tile has to be drawn for.
    /// </summary>
    private static void AddTop(SurfaceTool surface, int x, int y, float height, Color? colour)
    {
        Bounds(x, y, height, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);

        Quad(surface, a, b, c, d, colour,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
    }

    /// <summary>
    /// The side walls of a raised cell.
    ///
    /// Not decoration. Without them a raised cell is a floating lid with the
    /// background showing through underneath, which reads as black holes punched
    /// in the board -- clearly wrong the moment you look at a frame, and
    /// completely invisible to the compiler.
    /// </summary>
    private static void AddSides(SurfaceTool surface, int x, int y, float height, Color colour)
    {
        Bounds(x, y, height, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);

        float x0 = a.X, x1 = b.X, z0 = a.Z, z1 = d.Z;

        // Darkened so the raise reads as depth rather than as a colour change.
        Color side = colour.Darkened(0.35f);
        Quad(surface, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), b, a, side);   // north
        Quad(surface, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), c, b, side);   // east
        Quad(surface, new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), d, c, side);   // south
        Quad(surface, new Vector3(x0, 0, z1), new Vector3(x0, 0, z0), a, d, side);   // west
    }

    /// <summary>The four top corners of a cell, north-west first, clockwise.</summary>
    private static void Bounds(int x, int y, float height,
        out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
    {
        const float s = IsoGrid.CellSize;
        const float inset = 0.02f;   // a hairline gap so the grid reads as cells

        float x0 = x * s + inset, x1 = (x + 1) * s - inset;
        float z0 = y * s + inset, z1 = (y + 1) * s - inset;

        a = new Vector3(x0, height, z0);
        b = new Vector3(x1, height, z0);
        c = new Vector3(x1, height, z1);
        d = new Vector3(x0, height, z1);
    }

    private static void Quad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color? colour,
        Vector2 ua = default, Vector2 ub = default, Vector2 uc = default, Vector2 ud = default)
    {
        // Vertex colours are interpreted as LINEAR, but Color.FromHtml gives sRGB.
        // Without this conversion every terrain tone renders far lighter than the
        // hex it was authored as -- 55697d arriving on screen as a pale near-white.
        // That is why two rounds of palette edits changed almost nothing: the
        // values were fine, the colour space was wrong.
        if (colour is not null) surface.SetColor(colour.Value.SrgbToLinear());

        surface.SetUV(ua); surface.AddVertex(a);
        surface.SetUV(ub); surface.AddVertex(b);
        surface.SetUV(uc); surface.AddVertex(c);

        surface.SetUV(ua); surface.AddVertex(a);
        surface.SetUV(uc); surface.AddVertex(c);
        surface.SetUV(ud); surface.AddVertex(d);
    }
}
