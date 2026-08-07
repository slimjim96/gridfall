using System.Collections.Generic;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.View.Placeholders;

namespace Gridfall.Dev;

/// <summary>
/// The editor's status strip and validation panel.
///
/// Errors are the game validator's verdict shown early, warnings are MapTargets.
/// The panel reports; it never decides.
/// </summary>
public sealed partial class EditorHud : CanvasLayer
{
    private readonly Label _status = new();
    private readonly Label _brush = new();
    private readonly Label _panel = new();
    private readonly Label _estimate = new();
    private readonly Label _help = new();

    private const string HelpText =
        "1 buildable   2 path-only   3 blocked   4 spawn   5 goal\n" +
        "left drag paint   right drag erase   [ ] brush size   wheel zoom\n" +
        "ctrl+S save   ctrl+N new   ctrl+Z/Y undo\n" +
        "F4 cycle theme   F1 help   F2 routes   F3 panel   F5 playtest   F6 maze estimate   esc quit";

    public override void _Ready()
    {
        Place(_status, 16, 12, 20);
        Place(_brush, 16, 38, 16);
        Place(_panel, 16, 66, 15);
        Place(_estimate, 16, 0, 15);       // y set in ShowFindings, below the panel
        Place(_help, 16, 0, 14);
        _help.Modulate = new Color(1, 1, 1, 0.6f);
        _help.Text = HelpText;
        _help.Visible = false;

        SetBrush(CellKind.Buildable, 1, "slate");
        _status.Text = "board editor";
    }

    private void Place(Label label, int x, int y, int size)
    {
        label.Position = new Vector2(x, y);
        label.AddThemeFontSizeOverride("font_size", size);
        AddChild(label);
    }

    public void SetBrush(CellKind brush, int size, string theme)
        => _brush.Text = $"brush: {brush}   size {size}x{size}   theme: {theme}   (F1 for keys)";

    public void SetStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? Palette.Danger : Colors.White);
    }

    public void SetEstimate(string text) => _estimate.Text = text;

    public void ToggleHelp() => _help.Visible = !_help.Visible;
    public void TogglePanel() => _panel.Visible = !_panel.Visible;

    public void ShowFindings(List<MapFinding> findings, bool dirty, string mapId)
    {
        int errors = 0, warnings = 0;
        var lines = new List<string>();

        foreach (MapFinding f in findings)
        {
            switch (f.Severity)
            {
                case MapSeverity.Error: errors++; lines.Add($"  ERROR   {f}"); break;
                case MapSeverity.Warning: warnings++; lines.Add($"  warn    {f}"); break;
                case MapSeverity.Info: lines.Add($"  {f}"); break;
            }
        }

        string headline = errors > 0
            ? $"{errors} error(s) -- save refused"
            : warnings > 0 ? $"ok, {warnings} warning(s)" : "ok";

        _panel.Text = $"{mapId}{(dirty ? "*" : "")}   {headline}\n" + string.Join("\n", lines);
        _panel.AddThemeColorOverride("font_color", errors > 0 ? Palette.Danger : Colors.White);

        // Keep the estimate clear of a panel that grows with findings. Line
        // height is ~26px at this font size, not the 18 first guessed -- which
        // put the estimate straight through the last finding.
        const float lineHeight = 26f;
        float panelBottom = 66f + lineHeight * (lines.Count + 1);
        _estimate.Position = new Vector2(16, panelBottom + 10);
        _help.Position = new Vector2(16, panelBottom + 42);
    }
}
