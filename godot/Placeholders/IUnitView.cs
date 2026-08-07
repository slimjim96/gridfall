using Godot;

namespace Gridfall.View.Placeholders;

/// <summary>
/// What gameplay code needs from a visual, and nothing more.
///
/// ADR-0004: placeholders, 2D sprite sheets, and 3D meshes all sit behind this,
/// so the question of what Ludo.ai actually returns never reaches the code that
/// spawns a creep. Asset format becomes a per-entity data field rather than an
/// architectural commitment.
///
/// Deliberately a lowest common denominator. Sprite-only tricks (per-frame pivot
/// offsets) and mesh-only tricks (skeletal attachment points) both need an
/// explicit escape hatch rather than a widening of this interface.
/// </summary>
public interface IUnitView
{
    /// <summary>The node to parent into the scene. Owned by the view, not the caller.</summary>
    Node3D Node { get; }

    /// <summary>Continuous state, every frame, already interpolated.</summary>
    void SetWorldPosition(Vector3 position);

    /// <summary>
    /// Play a named clip. The standard set is idle / move / fire / hit / death.
    ///
    /// Duration comes from the asset, not the caller: sprite frames and mesh
    /// clips express it differently and gameplay must not have to know which.
    /// An unknown clip is ignored, not an error -- a placeholder legitimately
    /// has no "fire".
    /// </summary>
    void PlayClip(string clip);

    /// <summary>
    /// Tower level, 1-based. Added rather than expressed as a clip because level
    /// is a persistent STATE, not an event: a clip would replay on every reload
    /// and would not survive the view being recreated. Design rule "every
    /// player-visible state has a visible representation" makes it mandatory.
    /// </summary>
    void SetLevel(int level);

    /// <summary>
    /// Remaining structure health, 0-1. Persistent state like level, and for the
    /// same reason not a clip: a tower that reloads at half health must still
    /// look damaged.
    ///
    /// Mandatory for the same design rule. A tower can now be destroyed, and a
    /// destruction the player could not see coming is exactly the unexplainable
    /// loss pillar 4 forbids.
    /// </summary>
    void SetHealthFraction(float fraction);

    /// <summary>Per-frame view-side animation. Never feeds back into the sim.</summary>
    void Advance(float delta);

    /// <summary>Release nodes. Called when the entity is gone.</summary>
    void Dispose();
}
