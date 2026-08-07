using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;

namespace Gridfall.View;

/// <summary>
/// The projection contract from docs/iso-grid.md, in code, once.
///
/// Every constant describing how a grid coordinate becomes something on screen
/// lives here. If you find yourself typing 0.866 or 30 degrees anywhere else,
/// stop -- cite this instead. Changing a value here changes the doc too; they
/// are the same contract in two forms.
/// </summary>
public static class IsoGrid
{
    /// <summary>World units per cell.</summary>
    public const float CellSize = 1.0f;

    /// <summary>Degrees. 45 yaw + 30 pitch gives the 2:1 dimetric silhouette.</summary>
    public const float CameraYaw = 45.0f;

    /// <summary>
    /// Degrees, negative = looking down. NOT 35.264 (true isometric): 2:1 lands
    /// on clean pixel ratios and reads better at small tile sizes.
    /// </summary>
    public const float CameraPitch = -30.0f;

    public const float DefaultOrthoSize = 18.0f;
    public const float MinOrthoSize = 10.0f;
    public const float MaxOrthoSize = 30.0f;

    /// <summary>Ground decals sit here to avoid z-fighting with the terrain.</summary>
    public const float DecalHeight = 0.01f;

    /// <summary>Cells of slack beyond the board that the camera may pan to.</summary>
    public const float PanMarginCells = 2.0f;

    // ---- grid <-> world ---------------------------------------------------
    // The ground plane is XZ. Y is height.

    public static Vector3 GridToWorld(int x, int y, float height = 0f)
        => new(x * CellSize, height, y * CellSize);

    public static Vector3 GridToWorld(GridCell cell, float height = 0f)
        => GridToWorld(cell.X, cell.Y, height);

    /// <summary>Centre of a cell, which is where units stand.</summary>
    public static Vector3 CellCentre(int x, int y, float height = 0f)
        => new(x * CellSize + CellSize * 0.5f, height, y * CellSize + CellSize * 0.5f);

    public static GridCell WorldToGrid(Vector3 world)
        => new(Mathf.FloorToInt(world.X / CellSize), Mathf.FloorToInt(world.Z / CellSize));

    // ---- camera -----------------------------------------------------------

    /// <summary>
    /// Places an orthographic camera on the contract angles, framing the board.
    /// Rotation is never anything else: every asset's implied lighting assumes
    /// these angles.
    /// </summary>
    public static void ConfigureCamera(Camera3D camera, MapDef map, float orthoSize = 0f)
    {
        camera.Projection = Camera3D.ProjectionType.Orthogonal;
        camera.Size = orthoSize > 0f ? orthoSize : FitOrthoSize(map);
        camera.Near = 0.1f;
        camera.Far = 200.0f;

        Vector3 centre = BoardCentre(map);
        // Pull back along the view direction far enough that nothing clips.
        var basisRotation = Basis.FromEuler(new Vector3(
            Mathf.DegToRad(CameraPitch), Mathf.DegToRad(CameraYaw), 0f));
        Vector3 back = basisRotation * Vector3.Back;

        camera.Position = centre + back * 60.0f;
        camera.Basis = basisRotation;
    }

    public static Vector3 BoardCentre(MapDef map)
        => new(map.Width * CellSize * 0.5f, 0f, map.Height * CellSize * 0.5f);

    /// <summary>
    /// Ortho size that frames the whole board with a small margin.
    ///
    /// Rotated 45 degrees, a W x H board's on-screen diagonal is (W+H)*cos(45).
    /// Godot's Camera3D.Size is the VERTICAL extent, and the 30 degree pitch
    /// squashes that by sin(30), so the height needed is (W+H)*cos(45)*sin(30).
    ///
    /// The fixed 18 this replaced left the board using about half the frame on
    /// crossroads, which is wasted pixels on a game about reading the board.
    /// </summary>
    public static float FitOrthoSize(MapDef map, float viewportAspect = 16f / 9f)
    {
        const float margin = 1.28f;   // slack for the HUD strip and a breathing edge

        float span = (map.Width + map.Height) * CellSize;
        float width = span * Mathf.Cos(Mathf.DegToRad(45f));               // on-screen width
        float height = width * Mathf.Sin(Mathf.DegToRad(-CameraPitch));    // squashed by the pitch

        // Camera3D.Size is the VERTICAL extent, so a board wider than the
        // viewport's aspect is constrained by its width, not its height.
        // Ignoring that is how the first attempt cropped both ends off the board.
        float needed = Mathf.Max(height, width / viewportAspect);
        return Mathf.Clamp(needed * margin, MinOrthoSize, MaxOrthoSize);
    }

    /// <summary>
    /// Screen point to grid cell: a ray cast against the ground plane, solved
    /// analytically. Never a physics query -- the plane intersection is exact
    /// and free, and a per-frame space-state query is neither.
    ///
    /// Out-of-bounds is a valid answer ("no cell"), not an error.
    /// </summary>
    public static bool TryPick(Camera3D camera, Vector2 screenPoint, MapDef map, out GridCell cell)
        => TryPick(camera, screenPoint, map.Width, map.Height, out cell);

    /// <summary>Dimensions rather than a MapDef, for the editor's mutable draft.</summary>
    public static bool TryPick(Camera3D camera, Vector2 screenPoint, int width, int height, out GridCell cell)
    {
        Vector3 origin = camera.ProjectRayOrigin(screenPoint);
        Vector3 direction = camera.ProjectRayNormal(screenPoint);

        var ground = new Plane(Vector3.Up, 0f);
        Vector3? hit = ground.IntersectsRay(origin, direction);
        if (hit is null)
        {
            cell = GridCell.Invalid;
            return false;
        }

        cell = WorldToGrid(hit.Value);
        return cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height;
    }
}
