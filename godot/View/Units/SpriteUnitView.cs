using System.Collections.Generic;
using Godot;
using Gridfall.View.Placeholders;

namespace Gridfall.View.Units;

/// <summary>
/// A unit drawn as a camera-facing textured quad, animated from horizontal
/// sprite strips. ADR-0004's sprite half.
///
/// ## Why it occludes, and how easily it would stop
///
/// The material is <b>alpha-scissored, never alpha-blended</b>. Godot does not
/// write depth for a blended surface, and a surface that writes no depth hides
/// nothing behind it — the station would stop occluding visitors walking past it,
/// which is the whole reason iso-grid.md chose a 3D scene in the first place.
///
/// That makes a soft anti-aliased sprite edge a functional defect, not a
/// cosmetic one, and it is why `station-frost-spire.md` asks for a hard alpha edge
/// in the prompt itself. It cannot be fixed after the frames are cut.
///
/// ## Why a full billboard, and why the pivot is not height/2
///
/// The art already has the camera's foreshortening baked in — it was generated
/// at 45°/30°. Standing it on a vertical quad would foreshorten it a second
/// time and the unit would render squat. So the quad faces the camera exactly
/// (<see cref="BaseMaterial3D.BillboardModeEnum.Enabled"/>) and its size maps
/// 1:1 to the screen.
///
/// The pivot follows from that. The quad's centre must sit high enough that its
/// bottom edge lands on the cell centre *on screen*. Moving up `y` in world
/// space moves `y·cos(pitch)` on screen, so the offset is
/// `height / (2·cos(pitch))`, not `height / 2`. At the contract's 30° pitch that
/// is 15% more than the naive value — enough that every unit would look sunk
/// into the board.
/// </summary>
public sealed class SpriteUnitView : IUnitView
{
    /// <summary>Sprite strips are authored at the sim's tick rate. ludo-prompt-guide.md.</summary>
    private const float Fps = 30f;

    private sealed class Clip
    {
        public required Texture2D Texture { get; init; }
        public required int Frames { get; init; }
        public required bool Loops { get; init; }
    }

    private readonly Dictionary<string, Clip> _clips = new();
    private readonly MeshInstance3D _quad;
    private readonly StandardMaterial3D _material;
    private readonly QuadMesh _mesh;
    private readonly Color _baseColour = Colors.White;
    private readonly float _frameCells;

    private Clip? _current;
    private string _currentName = "";
    private float _elapsed;
    private int _level = 1;
    private float _healthFraction = 1f;

    public SpriteUnitView(UnitAsset asset)
    {
        _frameCells = asset.FrameCells;

        foreach (KeyValuePair<string, string> entry in asset.ClipStrips)
        {
            Image? image = Image.LoadFromFile(entry.Value);
            if (image is null)
            {
                GD.PrintErr($"units: could not read {entry.Value}");
                continue;
            }

            // Frames are square, so the count is implied by the strip's shape.
            // No sidecar metadata to drift out of sync with the image.
            int frames = image.GetHeight() > 0 ? Mathf.Max(1, image.GetWidth() / image.GetHeight()) : 1;

            _clips[entry.Key] = new Clip
            {
                Texture = ImageTexture.CreateFromImage(image),
                Frames = frames,
                Loops = UnitAssets.Loops(entry.Key),
            };
        }

        _mesh = new QuadMesh { Size = new Vector2(_frameCells, _frameCells) };

        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            // Scissor, not Alpha. See the class remarks -- this line is the
            // difference between a sprite that occludes and one that does not.
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlbedoColor = _baseColour,
        };

        _quad = new MeshInstance3D { Mesh = _mesh, MaterialOverride = _material };

        Node = new Node3D();
        Node.AddChild(_quad);

        ApplyTransform();
        Play("idle");
    }

    public Node3D Node { get; }

    public void SetWorldPosition(Vector3 position) => Node.Position = position;

    public void SetLevel(int level)
    {
        if (level == _level) return;
        _level = level;
        ApplyTransform();
        _material.AlbedoColor = CurrentTint;
    }

    public void SetHealthFraction(float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);
        if (Mathf.IsEqualApprox(fraction, _healthFraction)) return;
        _healthFraction = fraction;
        _material.AlbedoColor = CurrentTint;
    }

    /// <summary>An unknown clip is ignored, per IUnitView -- not an error.</summary>
    public void PlayClip(string clip) => Play(clip);

    public void Advance(float delta)
    {
        if (_current is null) return;

        _elapsed += delta;
        int frame = (int)(_elapsed * Fps);

        if (frame >= _current.Frames)
        {
            if (_current.Loops)
            {
                frame %= _current.Frames;
            }
            else
            {
                // A one-shot holds its last frame and then hands back to idle,
                // so a station is never left frozen mid-flare.
                frame = _current.Frames - 1;
                if (_currentName != "idle" && _clips.ContainsKey("idle")) { Play("idle"); return; }
            }
        }

        SetFrame(frame);
    }

    public void Dispose() => Node.QueueFree();

    // -----------------------------------------------------------------------

    private void Play(string clip)
    {
        if (!_clips.TryGetValue(clip, out Clip? next)) return;

        _current = next;
        _currentName = clip;
        _elapsed = 0f;
        _material.AlbedoTexture = next.Texture;
        _material.Uv1Scale = new Vector3(1f / next.Frames, 1f, 1f);
        SetFrame(0);
    }

    private void SetFrame(int frame)
    {
        if (_current is null) return;
        _material.Uv1Offset = new Vector3(frame / (float)_current.Frames, 0f, 0f);
    }

    /// <summary>
    /// Size and pivot. Level scales the quad, as with the placeholder, and the
    /// pivot is recomputed from it -- a taller sprite whose pivot did not follow
    /// would sink into the board as it upgraded.
    /// </summary>
    private void ApplyTransform()
    {
        float grow = 1f + 0.28f * (_level - 1);
        float height = _frameCells * grow;

        _mesh.Size = new Vector2(height, height);

        // height / (2 * cos(pitch)), from the class remarks. Read off IsoGrid so
        // it stays correct if the projection contract ever changes.
        float pitch = Mathf.DegToRad(-IsoGrid.CameraPitch);
        _quad.Position = new Vector3(0f, height / (2f * Mathf.Cos(pitch)), 0f);
    }

    /// <summary>
    /// Serving darkens and reddens, matching PlaceholderUnitView exactly. The
    /// cue must not depend on which asset format a unit happens to use -- a
    /// player learns the cue once.
    /// </summary>
    private Color CurrentTint
    {
        get
        {
            Color tint = _level > 1 ? _baseColour.Lightened(0.18f * (_level - 1)) : _baseColour;
            if (_healthFraction >= 1f) return tint;

            float hurt = 1f - _healthFraction;
            return tint.Lerp(Palette.Depleted, hurt * 0.9f).Darkened(hurt * 0.45f);
        }
    }
}
