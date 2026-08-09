using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;

namespace Gridfall.View;

/// <summary>
/// The projection contract from docs/iso-grid.md, in code, once.
///
/// Every constant describing how a grid coordinate becomes something on screen
/// patience here. If you find yourself typing 0.866 or 30 degrees anywhere else,
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

    /// <summary>
    /// World units per elevation level. See docs/iso-grid.md §Elevation.
    ///
    /// 0.22 sits just under the 0.28 a blocked cell is raised by, so a wall still
    /// reads as taller than the terrace it stands on rather than merging into it.
    /// </summary>
    public const float HeightStep = 0.22f;

    /// <summary>World height of an elevation level.</summary>
    public static float HeightOf(int level) => level * HeightStep;

    /// <summary>
    /// The height of the terrain surface at a cell — what anything standing on
    /// the board sits on top of.
    ///
    /// One definition, used by the renderer, the unit layer, the route overlay
    /// and every decal. A second copy is how a build preview ends up buried in a
    /// hillside while the station on the same cell floats above it.
    ///
    /// Does NOT include the kind raise (blocked +0.28, occupied +0.10). That is
    /// the renderer saying what is standing on the ground, not the ground itself.
    /// </summary>
    public static float TerrainHeight(MapDef map, int x, int y)
        => map.Heights.Length == 0 || x < 0 || y < 0 || x >= map.Width || y >= map.Height
            ? 0f
            : HeightOf(map.HeightAt(map.Index(x, y)));

    public static float TerrainHeight(MapDef map, GridCell cell) => TerrainHeight(map, cell.X, cell.Y);

    /// <summary>Cells of slack beyond the board that the camera may pan to.</summary>
    public const float PanMarginCells = 2.0f;

    // ---- screen-space directions -------------------------------------------
    // Panning needs to turn a mouse delta in pixels into a move across the
    // ground, which means knowing which way the world goes when the screen goes
    // right or up. Derived from the yaw rather than typed in, so they follow the
    // contract if it ever changes.

    private static float YawRadians => Mathf.DegToRad(CameraYaw);

    /// <summary>The ground direction that appears to point RIGHT on screen.</summary>
    public static Vector3 ScreenRight => new(Mathf.Cos(YawRadians), 0f, -Mathf.Sin(YawRadians));

    /// <summary>The ground direction that appears to point UP on screen.</summary>
    public static Vector3 ScreenUp => new(-Mathf.Sin(YawRadians), 0f, -Mathf.Cos(YawRadians));

    /// <summary>
    /// How much the ground plane is squashed vertically on screen: sin(pitch).
    ///
    /// A drag has to divide by this or vertical panning runs at half speed and
    /// the board visibly lags the cursor — the same 0.5 that FitOrthoSize uses
    /// to work out how tall a board renders.
    /// </summary>
    public static float GroundCompression => Mathf.Sin(Mathf.DegToRad(-CameraPitch));

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

        PointAt(camera, BoardCentre(map));
    }

    /// <summary>
    /// Aim the camera at a point on the ground, on the contract angles.
    ///
    /// Split out of ConfigureCamera so panning can move the focus without
    /// re-deriving the framing — and so the rotation is still written down once.
    /// </summary>
    public static void PointAt(Camera3D camera, Vector3 focus)
    {
        // Pull back along the view direction far enough that nothing clips.
        var basisRotation = Basis.FromEuler(new Vector3(
            Mathf.DegToRad(CameraPitch), Mathf.DegToRad(CameraYaw), 0f));
        Vector3 back = basisRotation * Vector3.Back;

        camera.Position = focus + back * 60.0f;
        camera.Basis = basisRotation;
    }

    /// <summary>
    /// Clamp a focus point to the board plus <see cref="PanMarginCells"/>.
    ///
    /// This is the rule docs/iso-grid.md has stated since it was written and
    /// nothing implemented: the board can be moved around, but never lost.
    /// </summary>
    public static Vector3 ClampFocus(Vector3 focus, MapDef map)
    {
        float margin = PanMarginCells * CellSize;
        return new Vector3(
            Mathf.Clamp(focus.X, -margin, map.Width * CellSize + margin),
            0f,
            Mathf.Clamp(focus.Z, -margin, map.Height * CellSize + margin));
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
        => TryPick(camera, screenPoint, map.Width, map.Height, map.Heights, out cell);

    /// <summary>Dimensions and heights rather than a MapDef, for the editor's mutable draft.</summary>
    public static bool TryPick(Camera3D camera, Vector2 screenPoint,
                               int width, int height, byte[] heights, out GridCell cell)
    {
        Vector3 origin = camera.ProjectRayOrigin(screenPoint);
        Vector3 direction = camera.ProjectRayNormal(screenPoint);

        bool flat = heights is null || heights.Length != width * height;
        float at = 0f;
        cell = GridCell.Invalid;

        // On a flat board this is one plane intersection, exactly as before.
        //
        // On an elevated one it has to iterate: a ray tested against y = 0 flies
        // over raised terrain and lands behind it -- about 1.7 cells per unit of
        // height at a 30-degree pitch -- so clicking a hilltop would select a
        // cell somewhere up the slope. Intersect, read the height of whatever
        // cell that hit, intersect again there. Converges in two or three passes
        // and stops as soon as it stops moving. Still only plane intersections:
        // no physics query, nothing worth measuring.
        //
        // See docs/iso-grid.md §Picking.
        for (int pass = 0; pass < 4; pass++)
        {
            Vector3? hit = new Plane(Vector3.Up, at).IntersectsRay(origin, direction);
            if (hit is null) return false;

            cell = WorldToGrid(hit.Value);
            bool inBounds = cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height;

            // Out of bounds is a real answer ("no cell"), and there is no height
            // to read there, so stop rather than loop against nothing.
            if (flat || !inBounds) return inBounds;

            float next = HeightOf(heights![cell.Y * width + cell.X]);
            if (Mathf.IsEqualApprox(next, at)) return true;
            at = next;
        }
        return true;
    }

    /// <summary>Flat-board pick. Kept for callers with no height field.</summary>
    public static bool TryPick(Camera3D camera, Vector2 screenPoint, int width, int height, out GridCell cell)
        => TryPick(camera, screenPoint, width, height, System.Array.Empty<byte>(), out cell);

}
