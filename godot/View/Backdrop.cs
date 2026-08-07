using Godot;
using Gridfall.Core.Content;

namespace Gridfall.View;

/// <summary>
/// The ground the board sits in: one large quad under and around it, tiling the
/// theme's `background/` image.
///
/// **Scenery, not board.** Nothing walks on it, nothing is built on it, and it
/// can never be clicked — `IsoGrid.TryPick` solves the ground plane analytically
/// and bounds-checks against the map, so no amount of backdrop can steal a pick.
/// It exists so a board reads as a place rather than as a slab floating in a
/// void, which is the last thing themes were missing.
///
/// A theme with no `background/` folder draws nothing here, and the scene's empty
/// colour shows through exactly as it did before backdrops existed.
/// </summary>
public sealed partial class Backdrop : Node3D
{
    /// <summary>
    /// How far below the board the surround sits.
    ///
    /// Not zero: at the same height it would z-fight the terrain, and it would
    /// also fill the hairline gaps between cells with something exactly as bright
    /// as the cells, erasing the grid. Set low enough that the board reads as a
    /// plateau standing in the landscape.
    /// </summary>
    private const float Depth = 0.35f;

    /// <summary>
    /// Cells per texture repeat. Deliberately coarser than the board's one-image-
    /// per-cell: a surround tiling at the same pitch as the grid reads as more
    /// playable board, and where the playable area ends is information the player
    /// needs at a glance.
    /// </summary>
    private const float CellsPerRepeat = 4.0f;

    /// <summary>
    /// How far past the board the quad runs. Generous on purpose — the camera
    /// zooms, and a backdrop that stops inside the viewport is worse than none.
    /// One quad, so the size costs nothing.
    /// </summary>
    private const float MarginCells = 140.0f;

    private readonly MeshInstance3D _plane = new();

    private string _builtTheme = "";
    private int _builtGeneration = -1;
    private int _builtWidth, _builtHeight;

    public override void _Ready() => AddChild(_plane);

    public void Initialise(MapDef map)
    {
        TerrainTile? background = TileLibrary.For(map.Theme)?.Background;

        if (background is null)
        {
            _plane.Visible = false;
            _builtTheme = "";
            return;
        }

        // The editor rebuilds the world on every brush stroke; the backdrop only
        // changes when the theme, the board size, or the tile library does.
        if (_builtTheme == map.Theme
            && _builtGeneration == TileLibrary.Generation
            && _builtWidth == map.Width
            && _builtHeight == map.Height)
        {
            _plane.Visible = true;
            return;
        }

        Build(map, background.Value);

        _builtTheme = map.Theme;
        _builtGeneration = TileLibrary.Generation;
        _builtWidth = map.Width;
        _builtHeight = map.Height;
    }

    private void Build(MapDef map, TerrainTile background)
    {
        const float s = IsoGrid.CellSize;

        float x0 = -MarginCells * s, x1 = (map.Width + MarginCells) * s;
        float z0 = -MarginCells * s, z1 = (map.Height + MarginCells) * s;

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        // UVs come from world position rather than from the quad's corners, so
        // the tiling is anchored to the grid: the surround does not slide when
        // the board is a different size.
        Quad(surface,
            new Vector3(x0, -Depth, z0), new Vector3(x1, -Depth, z0),
            new Vector3(x1, -Depth, z1), new Vector3(x0, -Depth, z1));

        surface.GenerateNormals();
        _plane.Mesh = surface.Commit();

        StandardMaterial3D material = Palette.Matte(Colors.White, unshaded: true);
        material.AlbedoTexture = background.Texture;
        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps;
        _plane.MaterialOverride = material;
        _plane.Visible = true;
    }

    private static void Quad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        AddVertex(surface, a);
        AddVertex(surface, b);
        AddVertex(surface, c);

        AddVertex(surface, a);
        AddVertex(surface, c);
        AddVertex(surface, d);
    }

    private static void AddVertex(SurfaceTool surface, Vector3 v)
    {
        surface.SetUV(new Vector2(v.X / (CellsPerRepeat * IsoGrid.CellSize),
                                  v.Z / (CellsPerRepeat * IsoGrid.CellSize)));
        surface.AddVertex(v);
    }
}
