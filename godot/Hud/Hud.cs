using Godot;
using Gridfall.Core;

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
    private readonly Label _repair = new();
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
        _help.Text = "left click: build   right click: sell   middle click: repair   " +
                     "space: start wave   1/2: tower   r: routes";
        AddChild(_help);

        // Sits under the help line, where the eye already is when hovering.
        _repair.Position = new Vector2(16, 92);
        _repair.AddThemeFontSizeOverride("font_size", 16);
        _repair.AddThemeColorOverride("font_color", Palette.Hint);
        _repair.Visible = false;
        AddChild(_repair);
    }

    /// <summary>
    /// What repairing the hovered tower would cost, or nothing if there is no
    /// damaged tower under the cursor.
    ///
    /// This is the discoverability claim: a player who can see a tower is hurt
    /// must also be able to see what fixing it costs, without a wiki. The caller
    /// passes the cost rather than computing it here -- the view must not own a
    /// second copy of the cost formula.
    /// </summary>
    public void ShowRepairPrompt(string? text)
    {
        _repair.Visible = text is not null;
        if (text is not null) _repair.Text = text;
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
            Core.Events.RejectReason.NotDamaged => "Not damaged.",
            Core.Events.RejectReason.WaveInProgress => "Not while a wave is running.",
            _ => "Can't do that.",
        };
        _refusal.Visible = true;
        _refusalRemaining = RefusalSeconds;
    }
}
