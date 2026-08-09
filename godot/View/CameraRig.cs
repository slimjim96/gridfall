using Godot;
using Gridfall.Core.Content;

namespace Gridfall.View;

/// <summary>
/// Where the camera is looking, and everything that moves it.
///
/// Owns a focus point on the ground plus an ortho size, and nothing else — the
/// pitch and yaw are the projection contract's and are never touched
/// (docs/iso-grid.md). Both the game and the board editor drive the same rig, so
/// panning cannot behave one way while you paint and another way while you play.
///
/// ## Why this exists as a class rather than a few lines in each scene
///
/// Three things have to agree and were previously nowhere: the clamp
/// (`PanMarginCells`, declared for months and read by nothing), the fact that a
/// rebuild must not throw the focus away, and the fact that shot mode must not
/// move at all. Two scenes implementing that independently would drift, and the
/// third rule is the one that silently invalidates every committed baseline.
/// </summary>
public sealed partial class CameraRig : Node
{
    /// <summary>
    /// Multiplicative zoom, not additive.
    ///
    /// The old editor stepped Camera3D.Size by a flat 1.5, which is 5% of the
    /// zoomed-out range and 15% of the zoomed-in one — so the same notch felt
    /// tiny at one end and lurched at the other. A ratio feels identical
    /// everywhere. 1.06 gives ~19 notches across the 10..30 range, which is the
    /// "slight" the request asked for.
    /// </summary>
    private const float ZoomStep = 1.06f;

    /// <summary>Cells per second for keyboard and edge panning, at default zoom.</summary>
    private const float KeyPanCellsPerSecond = 14.0f;

    /// <summary>How close to the viewport edge the cursor pans, in pixels.</summary>
    private const float EdgeBand = 24.0f;

    private Camera3D _camera = null!;
    private MapDef _map = null!;   // set in Initialise, before any use

    private Vector3 _focus;
    private bool _dragging;

    /// <summary>
    /// True in shot mode. Every capture baseline depends on the camera framing
    /// the board identically each run, so a locked rig ignores input entirely
    /// rather than trusting each caller to remember.
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Cursor-at-the-edge panning. On in the game, OFF in the board editor:
    /// painting a border wall means holding the cursor at the edge on purpose,
    /// and a camera that slides away while you do it is unusable.
    /// </summary>
    public bool EdgeScroll { get; set; } = true;

    public void Initialise(Camera3D camera, MapDef map)
    {
        _camera = camera;
        _map = map;
        IsoGrid.ConfigureCamera(camera, map);
        Recentre();
    }

    /// <summary>
    /// The map changed shape. Keep looking where the user was looking.
    ///
    /// The board editor rebuilds the world on every brush stroke, and the first
    /// version of that called ConfigureCamera each time — which would have
    /// snapped the view back to centre every cell you painted.
    /// </summary>
    public void Reframe(MapDef map)
    {
        _map = map;
        Apply();   // new bounds, same focus and same zoom -- just re-clamp
    }

    public void Recentre()
    {
        _focus = IsoGrid.BoardCentre(_map);
        Apply();
    }

    /// <summary>Discrete input. Returns true when the rig consumed the event.</summary>
    public bool HandleInput(InputEvent @event)
    {
        if (Locked) return false;

        if (@event is InputEventMouseButton button)
        {
            switch (button.ButtonIndex)
            {
                case MouseButton.WheelUp: Zoom(1f / ZoomStep); return true;
                case MouseButton.WheelDown: Zoom(ZoomStep); return true;
                case MouseButton.Middle: _dragging = button.Pressed; return true;
            }
            return false;
        }

        if (@event is InputEventMouseMotion motion && _dragging)
        {
            DragBy(motion.Relative);
            return true;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false } key && key.Keycode == Key.Home)
        {
            Recentre();
            return true;
        }

        return false;
    }

    /// <summary>Continuous input: held keys and the screen edge. Call once a frame.</summary>
    public void Update(float delta)
    {
        if (Locked) return;

        var move = Vector2.Zero;

        if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A)) move.X -= 1f;
        if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D)) move.X += 1f;
        if (Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.W)) move.Y += 1f;
        if (Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.S)) move.Y -= 1f;

        if (EdgeScroll) move += EdgePush();

        if (move == Vector2.Zero) return;

        // Speed scales with zoom, so a step crosses the same fraction of what you
        // can see whether you are zoomed in or out.
        float speed = KeyPanCellsPerSecond * (_camera.Size / IsoGrid.DefaultOrthoSize) * delta;
        PanBy(move.Normalized() * speed);
    }

    // -----------------------------------------------------------------------

    /// <summary>Which way the cursor is pushing, or zero when it is not near an edge.</summary>
    private Vector2 EdgePush()
    {
        Viewport viewport = GetViewport();
        if (viewport is null) return Vector2.Zero;

        Vector2 size = viewport.GetVisibleRect().Size;
        Vector2 mouse = viewport.GetMousePosition();

        // A cursor outside the window is not pushing anything. Without this a
        // window you clicked away from pans forever.
        if (mouse.X < 0f || mouse.Y < 0f || mouse.X > size.X || mouse.Y > size.Y) return Vector2.Zero;

        var push = Vector2.Zero;
        if (mouse.X < EdgeBand) push.X -= 1f;
        if (mouse.X > size.X - EdgeBand) push.X += 1f;
        if (mouse.Y < EdgeBand) push.Y += 1f;
        if (mouse.Y > size.Y - EdgeBand) push.Y -= 1f;
        return push;
    }

    /// <summary>Move the focus in screen-relative cells: +X right, +Y up.</summary>
    private void PanBy(Vector2 screenCells)
    {
        _focus += IsoGrid.ScreenRight * screenCells.X + IsoGrid.ScreenUp * screenCells.Y;
        Apply();
    }

    /// <summary>
    /// Drag the board with the cursor.
    ///
    /// The board must track the cursor exactly, so the focus moves against the
    /// mouse, and the vertical component is divided by the ground compression —
    /// a world step along ScreenUp only covers sin(pitch) of the screen, so
    /// without it the board drifts behind the cursor at half speed.
    /// </summary>
    private void DragBy(Vector2 pixels)
    {
        Viewport viewport = GetViewport();
        if (viewport is null) return;

        float unitsPerPixel = _camera.Size / viewport.GetVisibleRect().Size.Y;

        _focus -= IsoGrid.ScreenRight * (pixels.X * unitsPerPixel);
        _focus += IsoGrid.ScreenUp * (pixels.Y * unitsPerPixel / IsoGrid.GroundCompression);
        Apply();
    }

    private void Zoom(float factor)
    {
        _camera.Size = Mathf.Clamp(_camera.Size * factor, IsoGrid.MinOrthoSize, IsoGrid.MaxOrthoSize);
        Apply();
    }

    private void Apply()
    {
        _focus = IsoGrid.ClampFocus(_focus, _map);
        IsoGrid.PointAt(_camera, _focus);
    }
}
