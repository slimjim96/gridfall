using Godot;
using Gridfall.Core;
using Gridfall.View.Placeholders;

namespace Gridfall.View.Hud;

/// <summary>
/// Gold, lives, wave, and the refusal message. Reads state; changes nothing.
/// Built in code -- no .tscn to drift out of sync with it.
/// </summary>
public sealed partial class Hud : CanvasLayer
{
    private readonly Label _stats = new();
    private readonly Label _refusal = new();
    private readonly Label _help = new();
    private float _refusalRemaining;

    private const float RefusalSeconds = 1.6f;

    public override void _Ready()
    {
        _stats.Position = new Vector2(16, 12);
        _stats.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_stats);

        _refusal.Position = new Vector2(16, 40);
        _refusal.AddThemeFontSizeOverride("font_size", 18);
        _refusal.AddThemeColorOverride("font_color", Palette.Danger);
        _refusal.Visible = false;
        AddChild(_refusal);

        _help.Position = new Vector2(16, 70);
        _help.AddThemeFontSizeOverride("font_size", 14);
        _help.Modulate = new Color(1, 1, 1, 0.55f);
        _help.Text = "left click: build   right click: sell   space: start wave   1/2: tower   r: routes";
        AddChild(_help);
    }

    public void Refresh(SimStateView state, string towerName, float delta)
    {
        _stats.Text = $"gold {state.Gold}    lives {state.Lives}    wave {state.WaveIndex}    " +
                      $"creeps {state.CreepCount}    towers {state.TowerCount}    [{towerName}]";

        if (_refusalRemaining <= 0f) return;
        _refusalRemaining -= delta;
        if (_refusalRemaining <= 0f) _refusal.Visible = false;
    }

    /// <summary>
    /// Every refusal gets a visible message. A refused build that shows nothing
    /// reads as an unresponsive game (art-direction.md).
    /// </summary>
    public void ShowRefusal(Core.Events.RejectReason reason)
    {
        _refusal.Text = reason switch
        {
            Core.Events.RejectReason.WouldSealLane => "That would block the only route.",
            Core.Events.RejectReason.InsufficientGold => "Not enough gold.",
            Core.Events.RejectReason.NotBuildable => "You can't build there.",
            Core.Events.RejectReason.Occupied => "Something is already there.",
            Core.Events.RejectReason.OutOfBounds => "Off the board.",
            Core.Events.RejectReason.CapacityExceeded => "Too many towers.",
            _ => "Can't do that.",
        };
        _refusal.Visible = true;
        _refusalRemaining = RefusalSeconds;
    }
}
