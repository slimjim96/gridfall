using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Path;

namespace Gridfall.View;

/// <summary>
/// Draws the route creeps take, and the route they *would* take if you built on
/// the cell under the cursor.
///
/// Pillar 1 says the maze is the game. Until now the player could place a tower
/// and infer the consequence from watching creeps afterwards; this makes the
/// consequence visible before committing, which is the difference between a
/// puzzle and a guess.
///
/// Reads the flow field. Computes nothing itself -- the preview comes from the
/// same block check the simulation runs on release, so what you see and what you
/// get cannot disagree.
/// </summary>
public sealed partial class RouteOverlay : Node3D
{
    private const float LiveHeight = IsoGrid.DecalHeight + 0.002f;
    private const float PreviewHeight = IsoGrid.DecalHeight + 0.004f;
    private const int MaxRouteCells = 4096;

    private readonly MeshInstance3D _live = new();
    private readonly MeshInstance3D _preview = new();
    private readonly int[] _routeBuffer = new int[MaxRouteCells];

    private MapDef _map = null!;
    private PathSystem _path = null!;

    private ushort _liveBuiltForVersion = ushort.MaxValue;
    private int _previewBuiltForCell = -2;
    private bool _previewLegal;

    public bool Visible3D { get; private set; } = true;

    public override void _Ready()
    {
        AddChild(_live);
        AddChild(_preview);
    }

    public void Initialise(MapDef map, PathSystem path)
    {
        _map = map;
        _path = path;
        RebuildLiveIfChanged();
    }

    public void Toggle()
    {
        Visible3D = !Visible3D;
        _live.Visible = Visible3D;
        _preview.Visible = Visible3D && _previewBuiltForCell >= 0;
    }

    /// <summary>Cheap every frame: returns immediately unless the field moved.</summary>
    public void RebuildLiveIfChanged()
    {
        if (_path.Version == _liveBuiltForVersion) return;

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        bool any = false;

        foreach (GridCell spawn in _map.Spawns)
        {
            int count = _path.TraceRoute(_map.Index(spawn), _routeBuffer);
            for (int i = 0; i < count; i++)
            {
                AddPip(surface, _routeBuffer[i], LiveHeight, Palette.RouteLive, 0.26f);
                any = true;
            }
        }

        _live.Mesh = any ? Commit(surface) : null;
        _live.MaterialOverride = UnshadedVertexColour();
        _liveBuiltForVersion = _path.Version;

        // The preview was computed against the old field; it is stale now.
        ClearPreview();
    }

    /// <summary>
    /// Show the route that would result from building on this cell.
    ///
    /// Runs one BFS per hover-cell change, not per frame -- at ~4,096 cells that
    /// is well under a millisecond, but per frame it would be wasteful for a
    /// result that cannot have changed.
    /// </summary>
    public void ShowPreviewFor(GridCell cell)
    {
        int index = _map.Index(cell);
        if (index == _previewBuiltForCell) return;

        _previewBuiltForCell = index;
        _previewLegal = _path.WouldRemainConnected(index);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        bool any = false;

        Color colour = _previewLegal ? Palette.RoutePreview : Palette.Danger;

        if (_previewLegal)
        {
            foreach (GridCell spawn in _map.Spawns)
            {
                int count = _path.TraceRoute(_map.Index(spawn), _routeBuffer, preview: true);
                for (int i = 0; i < count; i++)
                {
                    AddPip(surface, _routeBuffer[i], PreviewHeight, colour, 0.34f);
                    any = true;
                }
            }
        }
        else
        {
            // Sealing: there is no route to draw, so mark the offending cell.
            // Silence would read as "nothing happened" rather than "refused".
            AddPip(surface, index, PreviewHeight, colour, 0.80f);
            any = true;
        }

        _preview.Mesh = any ? Commit(surface) : null;
        _preview.MaterialOverride = UnshadedVertexColour();
        _preview.Visible = Visible3D;
    }

    public void ClearPreview()
    {
        _previewBuiltForCell = -2;
        _preview.Mesh = null;
        _preview.Visible = false;
    }

    /// <summary>
    /// Drop the drawn route entirely.
    ///
    /// For the editor: a draft with no goal has no flow field, so there is no
    /// route to draw and leaving the last one up would show creeps walking to a
    /// goal that is not there any more.
    /// </summary>
    public void Clear()
    {
        _live.Mesh = null;
        _liveBuiltForVersion = ushort.MaxValue;   // force a rebuild once a goal returns
        ClearPreview();
    }

    // -----------------------------------------------------------------------

    private void AddPip(SurfaceTool surface, int cellIndex, float height, Color colour, float size)
    {
        int x = cellIndex % _map.Width;
        int y = cellIndex / _map.Width;

        float half = size * 0.5f * IsoGrid.CellSize;
        Vector3 centre = IsoGrid.CellCentre(x, y, height);

        var a = new Vector3(centre.X - half, height, centre.Z - half);
        var b = new Vector3(centre.X + half, height, centre.Z - half);
        var c = new Vector3(centre.X + half, height, centre.Z + half);
        var d = new Vector3(centre.X - half, height, centre.Z + half);

        // sRGB -> linear, same trap as the terrain: authored hex is sRGB but
        // vertex colours are read as linear.
        surface.SetColor(colour.SrgbToLinear());
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(d);
    }

    private static Mesh Commit(SurfaceTool surface)
    {
        surface.GenerateNormals();
        return surface.Commit();
    }

    private static StandardMaterial3D UnshadedVertexColour()
    {
        StandardMaterial3D material = Palette.Matte(Colors.White, unshaded: true);
        material.VertexColorUseAsAlbedo = true;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        return material;
    }
}
