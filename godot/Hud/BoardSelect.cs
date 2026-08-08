using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Gridfall.Core.Content;
using Gridfall.Io;

namespace Gridfall.View.Hud;

/// <summary>
/// Pick a board. Shown before the first run and again when a run ends.
///
/// `run-structure` chose option A — a run is one board, self-contained, lives
/// reset — but the map was a `const` in GameplayScene, so `gauntlet` shipped
/// unreachable and "selectable" was true only in the requirements doc. This is
/// the selector that makes it true.
///
/// Deliberately a keypress list, not a menu system. The filesystem is the map
/// manager (the same rule the board editor follows), so a map dropped into
/// content-data/maps/ appears here with no registration.
/// </summary>
public sealed partial class BoardSelect : CanvasLayer
{
    private readonly Label _label = new();
    private readonly List<string> _maps = new();

    /// <summary>Chosen id, or null while the player has not chosen.</summary>
    public string? Chosen { get; private set; }

    public override void _Ready()
    {
        _label.AddThemeFontSizeOverride("font_size", 22);
        _label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_label);
    }

    /// <summary>
    /// Scan content-data/maps/ and show the list. `heading` carries the run
    /// result, so the end of one run and the choice of the next are one screen
    /// rather than two.
    /// </summary>
    public void Open(string repoRoot, string heading)
    {
        _maps.Clear();
        Chosen = null;

        string dir = Path.Combine(repoRoot, "content-data", "maps");
        foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, System.StringComparer.Ordinal))
            _maps.Add(Path.GetFileNameWithoutExtension(file));

        var lines = new List<string> { heading, "" };
        for (int i = 0; i < _maps.Count && i < 9; i++)
        {
            // Size and theme read off the map itself: enough to choose by, and
            // it cannot go stale the way a hand-written list would.
            string detail = "";
            try
            {
                MapDef map = ContentFiles.LoadMap(repoRoot, _maps[i]);
                detail = $"   {map.Width}x{map.Height}  {map.Theme}";
            }
            catch (ContentException ex)
            {
                detail = $"   unreadable: {ex.Message}";
            }
            lines.Add($"{i + 1}   {_maps[i]}{detail}");
        }

        lines.Add("");
        lines.Add("number to play    esc to quit");

        _label.Text = string.Join("\n", lines);
        Visible = true;
    }

    /// <summary>True when the event was a choice. Read <see cref="Chosen"/> after.</summary>
    public bool HandleKey(InputEventKey key)
    {
        var index = (int)(key.Keycode - Key.Key1);
        if (index < 0 || index >= _maps.Count || index > 8) return false;

        Chosen = _maps[index];
        Visible = false;
        return true;
    }
}
