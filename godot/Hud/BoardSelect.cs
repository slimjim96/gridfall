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
///
/// That promise was false for a while. Slots were `1`–`9` and the list held
/// twelve maps, so `spiral`, `stepwell` and `switchback` existed, validated and
/// balanced, and could not be reached — silently, because the list simply
/// stopped. Slots now run `1`–`9` then `a`–`z`, and anything past the
/// thirty-fifth says so on screen rather than vanishing.
///
/// The cap is one number in one place on purpose: the bug was two independent
/// literals, a `i &lt; 9` in the drawing and an `index &gt; 8` in the key handling,
/// which is a pair that can disagree. <see cref="_maps"/> is truncated at the
/// scan and both now bound against it.
/// </summary>
public sealed partial class BoardSelect : CanvasLayer
{
    /// <summary>Addressable slots: `1`–`9`, then `a`–`z`.</summary>
    private const int Slots = 9 + 26;

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
        string[] files = Directory.GetFiles(dir, "*.json")
            .OrderBy(f => f, System.StringComparer.Ordinal)
            .ToArray();
        foreach (string file in files.Take(Slots))
            _maps.Add(Path.GetFileNameWithoutExtension(file));

        var lines = new List<string> { heading, "" };
        for (int i = 0; i < _maps.Count; i++)
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
            lines.Add($"{SlotLabel(i)}   {_maps[i]}{detail}");
        }

        // Loud rather than silent. Dropping off the end is what went wrong the
        // first time, and a map that cannot be played should at least be counted.
        if (files.Length > Slots)
            lines.Add($"    ...and {files.Length - Slots} more, past the last slot");

        lines.Add("");
        lines.Add("key to play    esc to quit");

        _label.Text = string.Join("\n", lines);
        Visible = true;
    }

    /// <summary>True when the event was a choice. Read <see cref="Chosen"/> after.</summary>
    public bool HandleKey(InputEventKey key)
    {
        int index = SlotIndex(key.Keycode);
        if (index < 0 || index >= _maps.Count) return false;

        Chosen = _maps[index];
        Visible = false;
        return true;
    }

    /// <summary>The key that plays slot <paramref name="index"/>.</summary>
    private static string SlotLabel(int index) =>
        index < 9 ? ((char)('1' + index)).ToString() : ((char)('a' + index - 9)).ToString();

    /// <summary>
    /// The slot a keycode addresses, or -1. The inverse of <see cref="SlotLabel"/>
    /// — the screen is modal while it is open, so no letter is spoken for.
    /// </summary>
    private static int SlotIndex(Key keycode)
    {
        if (keycode >= Key.Key1 && keycode <= Key.Key9) return (int)(keycode - Key.Key1);
        if (keycode >= Key.A && keycode <= Key.Z) return 9 + (int)(keycode - Key.A);
        return -1;
    }
}
