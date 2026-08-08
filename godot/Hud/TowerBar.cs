using System.Collections.Generic;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;

namespace Gridfall.View.Hud;

/// <summary>
/// The tower palette: one slot per tower this board offers, in roster order,
/// with its number key, its colour and its price right now.
///
/// The roster comes from <see cref="MapDef.TowerIds"/> and the bar asks
/// <see cref="MapDef.Offers"/> rather than filtering the list itself. That is the
/// whole point of the widget: **a slot on screen and a build the sim will accept
/// are the same set**, decided once, in Core. A toolbar that drew its own
/// conclusion would eventually offer a tower that gets refused, which is the
/// refusal message the player can do least about.
///
/// Prices are passed in per frame rather than computed here. The mid-wave premium
/// lives in CommandSystem.BuildCost and the view must not carry a second copy of
/// a formula it does not own.
///
/// Built in code, like the rest of the HUD -- no .tscn to drift out of sync.
/// </summary>
public sealed partial class TowerBar : Control
{
    private const int SlotSize = 46;

    private static readonly Color Ink = new(0.87f, 0.91f, 0.95f);
    private static readonly Color Dim = new(0.56f, 0.63f, 0.70f);
    private static readonly Color Faint = new(0.44f, 0.50f, 0.57f);

    private sealed class Slot
    {
        public required ushort DefIndex;
        public required PanelContainer Frame;
        public required ColorRect Chip;
        public required Label Name;
        public required Label Cost;
        public required Label Key;
    }

    private readonly List<Slot> _slots = new();

    /// <summary>Tower indices in roster order — the number-key order too.</summary>
    public IReadOnlyList<ushort> Order
    {
        get
        {
            var ids = new List<ushort>(_slots.Count);
            foreach (Slot s in _slots) ids.Add(s.DefIndex);
            return ids;
        }
    }

    /// <summary>
    /// Build one slot per offered tower. Called once, after content is loaded:
    /// the roster is part of the map and cannot change during a run.
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FitToViewport();
    }

    /// <summary>
    /// The bar's children anchor to THIS control's rect, and a Control parented to
    /// a CanvasLayer does not inherit one -- it stays 0x0 at the origin, so the
    /// whole bar lays out inside a zero box and renders as nothing. That looks
    /// exactly like the widget was never added, which cost a capture to diagnose.
    ///
    /// Taken from the viewport every frame rather than once, so it survives a
    /// window resize.
    /// </summary>
    private void FitToViewport()
    {
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
    }

    public void Populate(MapDef map, ContentSet content)
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", CardStyle());
        bar.AnchorLeft = 0.5f;
        bar.AnchorRight = 0.5f;
        bar.AnchorTop = 1.0f;
        bar.AnchorBottom = 1.0f;
        bar.GrowHorizontal = GrowDirection.Both;
        bar.GrowVertical = GrowDirection.Begin;
        bar.OffsetBottom = -16;
        AddChild(bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        bar.AddChild(row);

        for (ushort i = 0; i < content.Towers.Length; i++)
        {
            if (!map.Offers(content, i)) continue;
            TowerDef def = content.Tower(i);

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 2);
            column.Alignment = BoxContainer.AlignmentMode.Center;

            var frame = new PanelContainer { CustomMinimumSize = new Vector2(SlotSize, SlotSize) };
            frame.AddThemeStyleboxOverride("panel", SlotStyle(active: false));

            // A flat chip in the tower's own palette colour. The placeholder mesh
            // is a coloured solid too, so the swatch and the thing it builds are
            // the same colour by construction rather than by a lookup table
            // somebody has to remember to update.
            var chip = new ColorRect
            {
                Color = Palette.ForTower(def.Id),
                CustomMinimumSize = new Vector2(SlotSize - 14, SlotSize - 14),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            frame.AddChild(chip);

            Label key = Text(11, Faint);
            key.Text = (_slots.Count + 1).ToString();
            key.HorizontalAlignment = HorizontalAlignment.Center;

            Label name = Text(12, Dim);
            name.Text = def.Name;
            name.HorizontalAlignment = HorizontalAlignment.Center;

            Label cost = Text(13, Ink);
            cost.Text = def.Cost.ToString();
            cost.HorizontalAlignment = HorizontalAlignment.Center;

            column.AddChild(frame);
            column.AddChild(cost);
            column.AddChild(name);
            column.AddChild(key);
            row.AddChild(column);

            _slots.Add(new Slot
            {
                DefIndex = i, Frame = frame, Chip = chip,
                Name = name, Cost = cost, Key = key,
            });
        }

        IgnoreMouse(this);
    }

    /// <summary>
    /// Per frame: which slot is selected, what each costs right now, and what the
    /// player can currently afford.
    ///
    /// `costOf` is the sim's price including any mid-wave premium; `premium` says
    /// whether that price is currently inflated, so the bar can say so rather than
    /// silently showing a bigger number.
    /// </summary>
    public void Refresh(SimStateView state, ushort selected, System.Func<ushort, int> costOf, bool premium)
    {
        FitToViewport();

        foreach (Slot slot in _slots)
        {
            bool active = slot.DefIndex == selected;
            int cost = costOf(slot.DefIndex);
            bool affordable = state.Gold >= cost;

            slot.Frame.AddThemeStyleboxOverride("panel", SlotStyle(active));

            // Dim the chip, not the frame: the selected slot must stay findable
            // even while it is unaffordable, or the bar appears to lose the
            // player's selection every time they spend down.
            slot.Chip.Modulate = affordable ? Colors.White : new Color(1, 1, 1, 0.32f);

            slot.Cost.Text = premium ? $"{cost}+" : cost.ToString();
            slot.Cost.AddThemeColorOverride(
                "font_color", !affordable ? Faint : premium ? Palette.Hint : Ink);
            slot.Name.AddThemeColorOverride("font_color", active ? Ink : Dim);
        }
    }

    // ---- widgets ----------------------------------------------------------

    private static StyleBoxFlat CardStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.08f, 0.88f),
            BorderColor = new Color(1, 1, 1, 0.10f),
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 9,
            ContentMarginBottom = 9,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
        };
        style.SetBorderWidthAll(1);
        return style;
    }

    private static StyleBoxFlat SlotStyle(bool active)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.10f, 0.13f, 0.95f),
            BorderColor = active ? Ink : new Color(1, 1, 1, 0.14f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        style.SetBorderWidthAll(active ? 2 : 1);
        return style;
    }

    private static Label Text(int size, Color colour)
    {
        var label = new Label();
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        return label;
    }

    /// <summary>
    /// The bar sits over buildable cells at the bottom of the board. Without
    /// this it eats the clicks that would build there -- the same trap the editor
    /// HUD documents at length.
    /// </summary>
    private static void IgnoreMouse(Node node)
    {
        if (node is Control control) control.MouseFilter = MouseFilterEnum.Ignore;
        foreach (Node child in node.GetChildren()) IgnoreMouse(child);
    }
}
