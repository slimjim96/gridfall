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
    private readonly Label _runEnd = new();
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
        // "1-9" rather than "1/2": the number keys select the nth tower the BOARD
        // offers, and a board may offer one or five. The bar itself shows which
        // key is which, so this line only has to say that keys are how you pick.
        _help.Text = "left click: build   right click: sell   middle click: repair   " +
                     "space: start wave   1-9: tower   r: routes";
        AddChild(_help);

        // Centred, large, and the only thing on screen that ever stops the game.
        // A run that ends silently is the same defect as a loss you cannot
        // explain -- pillar 4 -- so the end state is the loudest thing here.
        _runEnd.AddThemeFontSizeOverride("font_size", 34);
        _runEnd.HorizontalAlignment = HorizontalAlignment.Center;
        _runEnd.VerticalAlignment = VerticalAlignment.Center;
        _runEnd.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _runEnd.Visible = false;
        AddChild(_runEnd);

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
    /// <summary>
    /// The run is over. `won` picks the colour, and nothing else in the HUD
    /// changes -- the final board stays readable underneath, because "why did I
    /// lose" is answered by looking at it.
    /// </summary>
    public void ShowRunEnd(string text, bool won)
    {
        _runEnd.Text = text;
        _runEnd.AddThemeColorOverride("font_color", won ? Palette.BuildPreviewOk : Palette.Danger);
        _runEnd.Visible = true;
    }

    public void ShowRepairPrompt(string? text)
    {
        _repair.Visible = text is not null;
        if (text is not null) _repair.Text = text;
    }

    /// <summary>
    /// `cost` is the price right now, premium included, and `premium` is true
    /// while a wave is running and that price is inflated.
    ///
    /// A price that silently changes is indistinguishable from a bug. The
    /// mid-wave premium exists to make reacting late a decision, and a decision
    /// the player cannot see the cost of is just a surprise -- pillar 4.
    /// </summary>
    public void Refresh(SimStateView state, string towerName, int cost, bool premium, float delta)
    {
        string price = premium ? $"{cost} +premium" : cost.ToString();

        _stats.Text = $"gold {state.Gold}    lives {state.Lives}    wave {state.WaveIndex}    " +
                      $"creeps {state.CreepCount}    towers {state.TowerCount}    " +
                      $"[{towerName} {price}]";

        // Amber, the "an offer, not a warning" slot -- the premium is a price to
        // weigh, not a refusal. Danger is reserved and would misread.
        _stats.AddThemeColorOverride("font_color", premium ? Palette.Hint : Colors.White);

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
