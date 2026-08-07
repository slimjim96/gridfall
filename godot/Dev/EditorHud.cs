using System.Collections.Generic;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.View;

namespace Gridfall.Dev;

/// <summary>
/// The editor's overlay: a status and validation rail down the left, a brush bar
/// along the bottom, and a help card in the middle.
///
/// Errors are the game validator's verdict shown early, warnings are MapTargets.
/// The panel reports; it never decides.
///
/// ## Why containers, not positions
///
/// The first version was five Labels at hand-computed y offsets, and the offsets
/// were wrong: the maze estimate was drawn straight through the last finding
/// because the guessed line height was 18px and the real one was 26. A panel
/// whose contents change length cannot have its layout typed in as numbers.
/// Everything here sizes itself, so a finding list of any length pushes what is
/// below it instead of colliding with it.
///
/// ## Why every control ignores the mouse
///
/// The editor picks cells in _UnhandledInput. A Control with the default
/// MouseFilter of Stop swallows the click first, so the board under the brush
/// bar would quietly stop being paintable. <see cref="IgnoreMouse"/> is applied
/// to every node added here, and must be applied to any node added later.
/// </summary>
public sealed partial class EditorHud : CanvasLayer
{
    private const int Margin = 16;
    private const int RailWidth = 340;

    private static readonly Color Ink = new(0.87f, 0.91f, 0.95f);
    private static readonly Color Dim = new(0.56f, 0.63f, 0.70f);
    private static readonly Color Faint = new(0.44f, 0.50f, 0.57f);

    // The brush order IS the number-key order. One list, so a swatch can never
    // drift out of step with the key that selects it.
    private static readonly (CellKind Kind, string Name)[] Brushes =
    {
        (CellKind.Buildable, "buildable"),
        (CellKind.PathOnly, "path-only"),
        (CellKind.Blocked, "blocked"),
        (CellKind.Spawn, "spawn"),
        (CellKind.Goal, "goal"),
    };

    private Label _mapName = null!;
    private Label _verdict = null!;
    private Label _message = null!;
    private PanelContainer _panelCard = null!;
    private VBoxContainer _findingRows = null!;
    private Label _estimate = null!;
    private Label _brushName = null!;
    private Label _brushSize = null!;
    private Label _themeName = null!;
    private PanelContainer _helpCard = null!;

    private readonly List<PanelContainer> _swatchFrames = new();
    private readonly List<TextureRect> _swatchImages = new();

    private const string HelpText =
        "1 2 3 4 5   brush: buildable / path-only / blocked / spawn / goal\n" +
        "left drag   paint            right drag  erase to buildable\n" +
        "[ ]         brush size       wheel       zoom\n" +
        "ctrl+S      save             ctrl+N      new map\n" +
        "ctrl+Z      undo             ctrl+shift+Z / ctrl+Y  redo\n" +
        "\n" +
        "F1  this help          F2  route overlay      F3  validation panel\n" +
        "F4  cycle theme        F5  playtest           F6  maze estimate\n" +
        "F7  reload tiles from presentation/tiles/\n" +
        "esc quit";

    public override void _Ready()
    {
        BuildRail();
        BuildBrushBar();
        BuildHelp();

        SetBrush(CellKind.Buildable, 1, TerrainTheme.Default);
        _mapName.Text = "board editor";
    }

    // ---- construction -----------------------------------------------------

    private void BuildRail()
    {
        var rail = new VBoxContainer { Position = new Vector2(Margin, Margin) };
        rail.AddThemeConstantOverride("separation", 8);
        AddChild(rail);

        // Status card: what map, is it dirty, and does it save.
        PanelContainer statusCard = Card();
        var statusBox = new VBoxContainer();
        statusCard.AddChild(statusBox);

        var headline = new HBoxContainer();
        headline.AddThemeConstantOverride("separation", 12);
        _mapName = Text(18, Ink);
        _verdict = Text(15, Dim);
        _verdict.HorizontalAlignment = HorizontalAlignment.Right;
        headline.AddChild(_mapName);
        headline.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        headline.AddChild(_verdict);
        statusBox.AddChild(headline);

        _message = Text(13, Dim);
        _message.Visible = false;
        statusBox.AddChild(_message);
        rail.AddChild(statusCard);

        // Validation card, F3.
        _panelCard = Card();
        var panelBox = new VBoxContainer();
        panelBox.AddThemeConstantOverride("separation", 6);
        _panelCard.AddChild(panelBox);

        panelBox.AddChild(SectionHeader("VALIDATION"));

        _findingRows = new VBoxContainer();
        _findingRows.AddThemeConstantOverride("separation", 3);
        panelBox.AddChild(_findingRows);

        panelBox.AddChild(new HSeparator());
        _estimate = Text(13, Faint);
        _estimate.Text = "maze estimate: F6";
        panelBox.AddChild(_estimate);

        rail.AddChild(_panelCard);
        IgnoreMouse(rail);
    }

    private void BuildBrushBar()
    {
        PanelContainer bar = Card();
        bar.AnchorLeft = 0.5f;
        bar.AnchorRight = 0.5f;
        bar.AnchorTop = 1.0f;
        bar.AnchorBottom = 1.0f;
        bar.GrowHorizontal = Control.GrowDirection.Both;
        bar.GrowVertical = Control.GrowDirection.Begin;
        bar.OffsetBottom = -Margin;
        bar.CustomMinimumSize = Vector2.Zero;
        AddChild(bar);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        bar.AddChild(box);

        var swatches = new HBoxContainer();
        swatches.AddThemeConstantOverride("separation", 8);
        swatches.Alignment = BoxContainer.AlignmentMode.Center;
        box.AddChild(swatches);

        for (int i = 0; i < Brushes.Length; i++)
        {
            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 2);
            column.Alignment = BoxContainer.AlignmentMode.Center;

            var frame = new PanelContainer { CustomMinimumSize = new Vector2(48, 48) };
            frame.AddThemeStyleboxOverride("panel", SwatchStyle(Colors.Black, active: false));

            var image = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            frame.AddChild(image);

            Label key = Text(11, Faint);
            key.Text = (i + 1).ToString();
            key.HorizontalAlignment = HorizontalAlignment.Center;

            column.AddChild(frame);
            column.AddChild(key);
            swatches.AddChild(column);

            _swatchFrames.Add(frame);
            _swatchImages.Add(image);
        }

        var caption = new HBoxContainer();
        caption.AddThemeConstantOverride("separation", 14);
        caption.Alignment = BoxContainer.AlignmentMode.Center;
        _brushName = Text(14, Ink);
        _brushSize = Text(13, Dim);
        _themeName = Text(13, Dim);
        caption.AddChild(_brushName);
        caption.AddChild(_brushSize);
        caption.AddChild(_themeName);
        box.AddChild(caption);

        IgnoreMouse(bar);
    }

    private void BuildHelp()
    {
        _helpCard = Card();
        _helpCard.AnchorLeft = 0.5f;
        _helpCard.AnchorRight = 0.5f;
        _helpCard.AnchorTop = 0.5f;
        _helpCard.AnchorBottom = 0.5f;
        _helpCard.GrowHorizontal = Control.GrowDirection.Both;
        _helpCard.GrowVertical = Control.GrowDirection.Both;
        _helpCard.CustomMinimumSize = Vector2.Zero;
        _helpCard.Visible = false;
        AddChild(_helpCard);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        _helpCard.AddChild(box);

        box.AddChild(SectionHeader("KEYS"));
        Label keys = Text(14, Ink);
        keys.Text = HelpText;
        box.AddChild(keys);

        IgnoreMouse(_helpCard);
    }

    // ---- updates ----------------------------------------------------------

    public void SetBrush(CellKind brush, int size, string theme)
    {
        ThemeTiles? tiles = TileLibrary.For(theme);
        TerrainTheme ramp = TerrainTheme.For(theme);

        for (int i = 0; i < Brushes.Length; i++)
        {
            CellKind kind = Brushes[i].Kind;
            bool active = kind == brush;

            Texture2D? tile = tiles?.Representative(kind);
            _swatchImages[i].Texture = tile;
            // Dim the inactive swatches so the selected brush is findable at a
            // glance rather than by reading the caption underneath.
            //
            // Darkened, not faded: alpha let the frame's colour-ramp background
            // through, so an inactive grass tile blended with slate and arrived
            // grey. A tile should never show a hue the theme does not have.
            float dim = active ? 1.0f : 0.62f;
            _swatchImages[i].Modulate = new Color(dim, dim, dim, 1.0f);

            _swatchFrames[i].AddThemeStyleboxOverride("panel", SwatchStyle(ramp.ColourFor(kind), active));
        }

        string name = "?";
        foreach ((CellKind kind, string label) in Brushes)
            if (kind == brush) name = label;

        _brushName.Text = name;
        _brushSize.Text = $"{size}x{size}";

        // Say where a tile theme comes from, and say when it is incomplete.
        // "theme: roadway" alone leaves you guessing whether the editor found
        // your folder at all; "(3 tiles)" on a theme missing every corner reads
        // as success while the board says otherwise.
        if (tiles is null)
        {
            _themeName.Text = $"theme: {theme} (colours, F4)";
            _themeName.AddThemeColorOverride("font_color", Dim);
        }
        else
        {
            _themeName.Text = tiles.IsComplete
                ? $"theme: {theme} ({tiles.TileCount} tiles, F4)"
                : $"theme: {theme} ({tiles.TileCount} tiles, {tiles.GapCount} gaps, F4)";
            _themeName.AddThemeColorOverride("font_color", tiles.IsComplete ? Dim : Palette.Hint);
        }
    }

    /// <summary>What a theme is missing, as one line, or empty when it is complete.</summary>
    public static string GapSummary(string theme)
    {
        ThemeTiles? tiles = TileLibrary.For(theme);
        return tiles is null || tiles.IsComplete ? "" : string.Join("; ", tiles.Gaps);
    }

    public void SetStatus(string text, bool error = false)
    {
        _message.Text = text;
        _message.Visible = text.Length > 0;
        _message.AddThemeColorOverride("font_color", error ? Palette.Danger : Dim);
    }

    public void SetEstimate(string text)
    {
        _estimate.Text = text;
    }

    public void ToggleHelp() => _helpCard.Visible = !_helpCard.Visible;

    public void TogglePanel() => _panelCard.Visible = !_panelCard.Visible;

    public void ShowFindings(List<MapFinding> findings, bool dirty, string mapId)
    {
        // Rebuilt rather than reformatted into one Label: a per-row Label is what
        // lets an error be red while the info line beside it stays plain. The old
        // single-Label panel turned every row red as soon as one row was an error.
        foreach (Node row in _findingRows.GetChildren())
        {
            _findingRows.RemoveChild(row);
            row.QueueFree();
        }

        int errors = 0, warnings = 0;

        foreach (MapFinding finding in findings)
        {
            (string glyph, Color colour) = finding.Severity switch
            {
                MapSeverity.Error => ("×", Palette.Danger),     // multiplication sign
                MapSeverity.Warning => ("!", Palette.Hint),
                _ => ("·", Faint),                              // middle dot
            };

            if (finding.Severity == MapSeverity.Error) errors++;
            if (finding.Severity == MapSeverity.Warning) warnings++;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            Label mark = Text(13, colour);
            mark.Text = glyph;
            mark.CustomMinimumSize = new Vector2(10, 0);
            mark.HorizontalAlignment = HorizontalAlignment.Center;

            Label body = Text(13, colour);
            body.Text = finding.ToString();
            body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            row.AddChild(mark);
            row.AddChild(body);
            _findingRows.AddChild(row);
        }

        if (findings.Count == 0)
        {
            Label none = Text(13, Faint);
            none.Text = "nothing to report";
            _findingRows.AddChild(none);
        }

        _mapName.Text = dirty ? $"{mapId} *" : mapId;

        (_verdict.Text, Color verdictColour) = errors > 0
            ? ($"× {errors} error{(errors == 1 ? "" : "s")}", Palette.Danger)
            : warnings > 0
                ? ($"! {warnings} warning{(warnings == 1 ? "" : "s")}", Palette.Hint)
                : ("ok", Palette.BuildPreviewOk);
        _verdict.AddThemeColorOverride("font_color", verdictColour);

        IgnoreMouse(_findingRows);
    }

    // ---- widgets ----------------------------------------------------------

    private static PanelContainer Card()
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(RailWidth, 0) };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.08f, 0.88f),
            BorderColor = new Color(1, 1, 1, 0.10f),
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 9,
            ContentMarginBottom = 9,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
        };
        style.SetBorderWidthAll(1);

        card.AddThemeStyleboxOverride("panel", style);
        return card;
    }

    private static StyleBoxFlat SwatchStyle(Color fill, bool active)
    {
        var style = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = active ? Ink : new Color(1, 1, 1, 0.14f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
        style.SetBorderWidthAll(active ? 2 : 1);
        return style;
    }

    private static Label SectionHeader(string text)
    {
        Label label = Text(11, Faint);
        label.Text = text;
        return label;
    }

    private static Label Text(int size, Color colour)
    {
        var label = new Label();
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
        // Placeholder art is flat and the panels are only 88% opaque, so a thin
        // shadow is what keeps 13px text legible where a card overlaps the board.
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        return label;
    }

    /// <summary>
    /// Stop the overlay eating board clicks. See the class remarks -- a
    /// PanelContainer defaults to MouseFilter.Stop, and the bottom brush bar sits
    /// directly over cells you need to paint.
    /// </summary>
    private static void IgnoreMouse(Node node)
    {
        if (node is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (Node child in node.GetChildren()) IgnoreMouse(child);
    }
}
