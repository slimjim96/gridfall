using System.Collections.Generic;
using Godot;
using Gridfall.View.Placeholders;

namespace Gridfall.View.Units;

/// <summary>
/// A unit drawn as a loaded glTF model, animated by its own AnimationPlayer.
/// ADR-0004's mesh half.
///
/// Occlusion, lighting and zoom all come free here — it is ordinary opaque 3D
/// geometry in a 3D scene, which is the case iso-grid.md was already built for.
/// Everything interesting in this class is instead about *not trusting the
/// asset*: a generated `.glb` arrives with whatever materials, scale and clip
/// names the generator felt like, and the view has to stay predictable anyway.
///
/// ## Tinting without destroying the asset's own materials
///
/// Level and damage tint by duplicating each surface's material and multiplying
/// its albedo, never by MaterialOverride — an override would replace whatever
/// texture the model shipped with, so a damaged tower would lose its art
/// entirely rather than darken.
/// </summary>
public sealed class MeshUnitView : IUnitView
{
    private readonly List<StandardMaterial3D> _tintable = new();
    private readonly AnimationPlayer? _animation;
    private readonly Node3D? _model;

    private int _level = 1;
    private float _healthFraction = 1f;

    public MeshUnitView(UnitAsset asset)
    {
        Node = new Node3D();

        _model = LoadModel(asset);
        if (_model is null) return;

        Node.AddChild(_model);

        _animation = FindAnimationPlayer(_model);
        CollectTintableMaterials(_model);

        PlayClip("idle");
    }

    public Node3D Node { get; }

    public void SetWorldPosition(Vector3 position) => Node.Position = position;

    public void SetLevel(int level)
    {
        if (level == _level) return;
        _level = level;

        // Same growth curve as the placeholder and the sprite view: height is
        // the primary level cue because it survives greyscale.
        float grow = 1f + 0.28f * (level - 1);
        if (_model is not null)
            _model.Scale = new Vector3(1f + 0.10f * (level - 1), grow, 1f + 0.10f * (level - 1));

        ApplyTint();
    }

    public void SetHealthFraction(float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);
        if (Mathf.IsEqualApprox(fraction, _healthFraction)) return;
        _healthFraction = fraction;
        ApplyTint();
    }

    /// <summary>
    /// An unknown clip is ignored, per IUnitView. A generated model that shipped
    /// with no "fire" must not throw the first time a tower shoots.
    /// </summary>
    public void PlayClip(string clip)
    {
        if (_animation is null || !_animation.HasAnimation(clip)) return;

        Animation? resource = _animation.GetAnimation(clip);
        if (resource is not null)
            resource.LoopMode = UnitAssets.Loops(clip) ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

        _animation.Play(clip);
    }

    /// <summary>Nothing per frame: AnimationPlayer advances itself inside the tree.</summary>
    public void Advance(float delta) { }

    public void Dispose() => Node.QueueFree();

    // -----------------------------------------------------------------------

    /// <summary>
    /// Load the `.glb` from an absolute path outside `res://`.
    ///
    /// GltfDocument at runtime rather than GD.Load, for the same reason tiles use
    /// Image.LoadFromFile: assets live outside the Godot project so that dropping
    /// one in needs no import step. GD.Load would require the file to be under
    /// res:// and already imported.
    /// </summary>
    private static Node3D? LoadModel(UnitAsset asset)
    {
        if (asset.ModelPath is null) return null;

        var document = new GltfDocument();
        var state = new GltfState();

        Error error = document.AppendFromFile(asset.ModelPath, state);
        if (error != Error.Ok)
        {
            GD.PrintErr($"units: {asset.ContentId} -- could not read {asset.ModelPath} ({error})");
            return null;
        }

        if (document.GenerateScene(state) is not Node3D scene)
        {
            GD.PrintErr($"units: {asset.ContentId} -- {asset.ModelPath} produced no 3D scene");
            return null;
        }

        return scene;
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer player) return player;
        foreach (Node child in node.GetChildren())
            if (FindAnimationPlayer(child) is AnimationPlayer found) return found;
        return null;
    }

    private void CollectTintableMaterials(Node node)
    {
        if (node is MeshInstance3D instance && instance.Mesh is not null)
        {
            for (int surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                // Duplicate: the loaded material may be shared between surfaces
                // or between instances, and tinting one damaged tower must not
                // tint every other tower of the same type.
                if (instance.GetActiveMaterial(surface) is not StandardMaterial3D source) continue;

                var copy = (StandardMaterial3D)source.Duplicate();

                // Normalise to the art direction rather than hoping the asset
                // already obeys it. art-direction.md is flat matte and the rest
                // of the board reaches that through Palette.Matte; a generated
                // .glb reaches it through nothing at all, and one glossy metallic
                // return would put the only specular highlight in the game on a
                // tower. Cheaper to enforce here than to police in every prompt.
                copy.Roughness = 1.0f;
                copy.Metallic = 0.0f;
                copy.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;

                instance.SetSurfaceOverrideMaterial(surface, copy);
                _tintable.Add(copy);
            }
        }

        foreach (Node child in node.GetChildren()) CollectTintableMaterials(child);
    }

    /// <summary>
    /// Multiplied onto whatever albedo the asset already has, so a textured model
    /// darkens rather than turning into a flat colour.
    ///
    /// Identical curve to PlaceholderUnitView and SpriteUnitView: the damage cue
    /// must not depend on which format a unit happens to ship in.
    /// </summary>
    private void ApplyTint()
    {
        Color tint = _level > 1 ? Colors.White.Lightened(0.18f * (_level - 1)) : Colors.White;

        if (_healthFraction < 1f)
        {
            float hurt = 1f - _healthFraction;
            tint = tint.Lerp(Palette.Damaged, hurt * 0.9f).Darkened(hurt * 0.45f);
        }

        foreach (StandardMaterial3D material in _tintable) material.AlbedoColor = tint;
    }
}
