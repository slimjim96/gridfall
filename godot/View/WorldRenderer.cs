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
    private readonly MeshInstance3D _hover = new();
    private MapDef _map = null!;
    private PathSystem _path = null!;
    private ushort _builtForVersion = ushort.MaxValue;

    public override void _Ready()
    {
        AddChild(_terrain);

        _hover.Mesh = Shapes.GroundQuad(IsoGrid.CellSize * 0.94f);
        _hover.MaterialOverride = Palette.Matte(Palette.BuildPreviewOk, unshaded: true);
        _hover.Visible = false;
        AddChild(_hover);
    }

    public void Initialise(MapDef map, PathSystem path)
    {
        _map = map;
        _path = path;
        Rebuild();
    }

    /// <summary>Cheap to call every frame: it returns immediately unless the grid moved.</summary>
    public void RebuildIfChanged()
    {
        if (_path.Version == _builtForVersion) return;
        Rebuild();
    }

    public void ShowHover(GridCell cell, bool legal)
    {
        _hover.Visible = true;
        _hover.Position = IsoGrid.CellCentre(cell.X, cell.Y, IsoGrid.DecalHeight);
        ((StandardMaterial3D)_hover.MaterialOverride).AlbedoColor =
            legal ? Palette.BuildPreviewOk : Palette.Danger;
    }

    public void HideHover() => _hover.Visible = false;

    private void Rebuild()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                int index = _map.Index(x, y);
                CellKind kind = _map.Cells[index];
                if (kind == CellKind.Blocked)
                {
                    AddCell(surface, x, y, 0.28f, Palette.TerrainBlocked);
                    continue;
                }

                // A tower occupies the cell: raise it so the maze is legible as
                // shape, not only as colour.
                bool occupied = _path.IsBlocked(index);
                Color colour = kind switch
                {
                    CellKind.Buildable => Palette.TerrainBuildable,
                    CellKind.PathOnly => Palette.TerrainPathOnly,
                    CellKind.Spawn => Palette.TerrainSpawn,
                    CellKind.Goal => Palette.TerrainGoal,
                    _ => Palette.TerrainBuildable,
                };

                AddCell(surface, x, y, occupied ? 0.10f : 0.0f, colour);
            }
        }

        surface.GenerateNormals();
        _terrain.Mesh = surface.Commit();

        // Unshaded on purpose. With lighting applied, ACES tonemapping plus ambient
        // compressed every terrain tone toward white and the palette stopped
        // meaning anything -- two separate colour passes changed nothing visible.
        // Unshaded makes the board render exactly the values art-direction.md
        // names, which is what a board you reason about should do. Units stay
        // shaded, so they read as objects sitting on it.
        StandardMaterial3D material = Palette.Matte(Colors.White, unshaded: true);
        material.VertexColorUseAsAlbedo = true;   // typed, not Set("...") by string
        _terrain.MaterialOverride = material;

        _builtForVersion = _path.Version;
    }

    /// <summary>
    /// A cell: a coloured top quad, plus side walls when it is raised.
    ///
    /// The sides are not decoration. Without them a raised cell is a floating
    /// lid with the background showing through underneath, which reads as black
    /// holes punched in the board -- clearly wrong the moment you look at a
    /// frame, and completely invisible to the compiler.
    /// </summary>
    private static void AddCell(SurfaceTool surface, int x, int y, float height, Color colour)
    {
        const float s = IsoGrid.CellSize;
        const float inset = 0.02f;   // a hairline gap so the grid reads as cells

        float x0 = x * s + inset, x1 = (x + 1) * s - inset;
        float z0 = y * s + inset, z1 = (y + 1) * s - inset;

        var a = new Vector3(x0, height, z0);
        var b = new Vector3(x1, height, z0);
        var c = new Vector3(x1, height, z1);
        var d = new Vector3(x0, height, z1);

        Quad(surface, a, b, c, d, colour);

        if (height <= 0f) return;

        // Sides, darkened so the raise reads as depth rather than as a colour change.
        Color side = colour.Darkened(0.35f);
        Quad(surface, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), b, a, side);   // north
        Quad(surface, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), c, b, side);   // east
        Quad(surface, new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), d, c, side);   // south
        Quad(surface, new Vector3(x0, 0, z1), new Vector3(x0, 0, z0), a, d, side);   // west
    }

    private static void Quad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
    {
        // Vertex colours are interpreted as LINEAR, but Color.FromHtml gives sRGB.
        // Without this conversion every terrain tone renders far lighter than the
        // hex it was authored as -- 55697d arriving on screen as a pale near-white.
        // That is why two rounds of palette edits changed almost nothing: the
        // values were fine, the colour space was wrong.
        surface.SetColor(colour.SrgbToLinear());
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);

        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(d);
    }
}
