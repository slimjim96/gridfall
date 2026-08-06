using Godot;

namespace Gridfall.View.Placeholders;

/// <summary>
/// A placeholder: one mesh, one colour, and the three shared motions from
/// placeholder-standard.md. No bespoke animation, no detail geometry, no
/// texture beyond a flat albedo.
///
/// Placeholders do not get individual animation. Idle bob, hit flash, and death
/// collapse are implemented once, here, and every placeholder shares them.
/// </summary>
public sealed class PlaceholderUnitView : IUnitView
{
    private const float BobAmplitude = 0.02f;   // 2% vertical, per the standard
    private const float BobSpeed = 2.4f;
    private const float HitFlashSeconds = 0.08f;
    private const float DeathCollapseSeconds = 0.15f;

    private readonly MeshInstance3D _mesh;
    private readonly StandardMaterial3D _material;
    private readonly Color _baseColour;
    private readonly float _restHeight;
    private readonly bool _bobs;
    private readonly float _phase;

    private float _time;
    private float _flashRemaining;
    private float _collapseRemaining;
    private bool _collapsing;

    public PlaceholderUnitView(Mesh mesh, Color colour, float restHeight, bool bobs, int phaseSeed)
    {
        _baseColour = colour;
        _restHeight = restHeight;
        _bobs = bobs;
        // Phase offset by entity id so a crowd does not pulse in unison.
        _phase = (phaseSeed % 32) / 32.0f * Mathf.Tau;

        _material = Palette.Matte(colour);
        _mesh = new MeshInstance3D { Mesh = mesh, MaterialOverride = _material };
        Node = new Node3D();
        Node.AddChild(_mesh);
        _mesh.Position = new Vector3(0, restHeight, 0);
    }

    public Node3D Node { get; }

    public bool IsFinished => _collapsing && _collapseRemaining <= 0f;

    public void SetWorldPosition(Vector3 position) => Node.Position = position;

    public void PlayClip(string clip)
    {
        switch (clip)
        {
            case "hit": _flashRemaining = HitFlashSeconds; break;
            case "death": _collapsing = true; _collapseRemaining = DeathCollapseSeconds; break;
            case "fire": _flashRemaining = HitFlashSeconds * 0.6f; break;
            // idle and move are implicit; an unknown clip is ignored on purpose.
        }
    }

    public void Advance(float delta)
    {
        _time += delta;

        if (_collapsing)
        {
            _collapseRemaining -= delta;
            float t = Mathf.Clamp(_collapseRemaining / DeathCollapseSeconds, 0f, 1f);
            _mesh.Scale = new Vector3(t, t, t);
            return;
        }

        if (_flashRemaining > 0f)
        {
            _flashRemaining -= delta;
            float t = Mathf.Clamp(_flashRemaining / HitFlashSeconds, 0f, 1f);
            _material.AlbedoColor = _baseColour.Lerp(Palette.HitFlash, t);
        }
        else if (_material.AlbedoColor != _baseColour)
        {
            _material.AlbedoColor = _baseColour;
        }

        if (!_bobs) return;
        float bob = Mathf.Sin(_time * BobSpeed * Mathf.Tau + _phase) * BobAmplitude;
        _mesh.Position = new Vector3(0, _restHeight + bob, 0);
    }

    public void Dispose()
    {
        Node.QueueFree();
    }
}
