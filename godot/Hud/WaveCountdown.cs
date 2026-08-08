using Godot;
using Gridfall.Core;

namespace Gridfall.View.Hud;

/// <summary>
/// The gap between waves, drawn: a ring that empties as the prep window runs out,
/// with the seconds left inside it.
///
/// **Pure view.** `PrepTicksRemaining` already exists in `SimState`, is already
/// hashed, and is already exposed through `SimStateView` — this reads it and
/// draws. Nothing here can change the simulation, and the countdown is therefore
/// exactly as deterministic as the thing it is counting.
///
/// It is also the answer to a note in `balance-targets.md`: prepTicks is recorded
/// there as *"a FEEL knob the sim cannot measure at any value"*, left at a
/// placeholder 300 to be tuned by playing. It could not be tuned by playing
/// because nothing on screen showed it. Now it does.
///
/// Deliberately plain geometry. This is a placeholder with an hour's budget and a
/// silhouette — an arc, a numeral, two lines of text — sized and positioned so a
/// generated asset can replace the drawing without moving anything around it.
/// </summary>
public sealed partial class WaveCountdown : Control
{
    private const float Radius = 54f;
    private const float Thickness = 7f;
    private const float FadeSeconds = 0.3f;

    private readonly Label _seconds = new();
    private readonly Label _caption = new();
    private readonly Label _prompt = new();

    private float _fraction;      // 1 at the start of the window, 0 when it fires
    private int _wholeSeconds;
    private float _fade = 1f;
    private bool _running;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _seconds.AddThemeFontSizeOverride("font_size", 40);
        _seconds.HorizontalAlignment = HorizontalAlignment.Center;
        _seconds.VerticalAlignment = VerticalAlignment.Center;
        _seconds.AddThemeColorOverride("font_color", Palette.Hint);
        Shadow(_seconds);
        AddChild(_seconds);

        _caption.AddThemeFontSizeOverride("font_size", 17);
        _caption.HorizontalAlignment = HorizontalAlignment.Center;
        _caption.AddThemeColorOverride("font_color", new Color(0.87f, 0.91f, 0.95f));
        Shadow(_caption);
        AddChild(_caption);

        _prompt.AddThemeFontSizeOverride("font_size", 13);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt.AddThemeColorOverride("font_color", new Color(0.56f, 0.63f, 0.70f));
        Shadow(_prompt);
        _prompt.Text = "space to start now";
        AddChild(_prompt);

        Visible = false;
    }

    /// <summary>
    /// `prepTicks` is the window this wave was armed with — the denominator. It
    /// comes from the wave def rather than from a remembered high-water mark,
    /// because calling a wave early zeroes the counter and a remembered maximum
    /// would make the next window start part-drained.
    /// </summary>
    public void Refresh(SimStateView state, int prepTicks, int ticksPerSecond, float delta)
    {
        _running = !state.WaveActive && state.PrepTicksRemaining > 0 && prepTicks > 0;

        if (_running)
        {
            _fade = 1f;
            _fraction = Mathf.Clamp(state.PrepTicksRemaining / (float)prepTicks, 0f, 1f);
            // Ceiling, so a window with any time left never reads "0" -- a zero
            // that lingers for a frame looks like the timer stalled.
            _wholeSeconds = Mathf.CeilToInt(state.PrepTicksRemaining / (float)ticksPerSecond);

            // WaveIndex is the wave about to run, and it is 0-based in state.
            _caption.Text = $"wave {state.WaveIndex + 1} incoming";
            _seconds.Text = _wholeSeconds.ToString();
            Visible = true;
        }
        else if (_fade > 0f)
        {
            // Fade rather than vanish: the ring is the only thing on screen that
            // disappears at the exact moment the board gets busy, and a hard cut
            // there reads as a dropped frame.
            _fade = Mathf.Max(0f, _fade - delta / FadeSeconds);
            _fraction = 0f;
            Visible = _fade > 0f;
        }

        if (!Visible) return;

        // A Control parented to a CanvasLayer has no rect of its own to anchor
        // against, so the viewport is the only honest source of "centre". Read
        // every frame so a resize cannot leave the ring off to one side.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;

        Modulate = new Color(1, 1, 1, _fade);
        Vector2 centre = Size * 0.5f;
        _seconds.Position = new Vector2(centre.X - 60, centre.Y - 26);
        _seconds.Size = new Vector2(120, 52);
        _caption.Position = new Vector2(centre.X - 140, centre.Y + Radius + 12);
        _caption.Size = new Vector2(280, 22);
        _prompt.Position = new Vector2(centre.X - 140, centre.Y + Radius + 34);
        _prompt.Size = new Vector2(280, 18);

        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 centre = Size * 0.5f;

        // A scrim behind everything. The ring sits over the middle of the board,
        // which is the busiest part of the frame and whose colour is whatever the
        // map's theme happens to be -- the numeral was competing with route
        // markers and a range preview at the same brightness. The disc is what
        // makes one widget legible on twelve palettes.
        DrawCircle(centre, Radius + Thickness, new Color(0.03f, 0.05f, 0.07f, 0.72f));

        // The track next, so the remaining arc always has something to be a
        // fraction OF. A bare arc on an empty background reads as a wedge and
        // gives no sense of how much of the window is gone.
        DrawArc(centre, Radius, 0f, Mathf.Tau, 96,
                new Color(1, 1, 1, 0.13f), Thickness, antialiased: true);

        if (_fraction <= 0f) return;

        // Clockwise from twelve o'clock: -pi/2 is up in Godot's screen space, and
        // a timer that runs anticlockwise reads as counting up.
        const float start = -Mathf.Pi / 2f;
        DrawArc(centre, Radius, start, start + Mathf.Tau * _fraction, 96,
                Palette.Hint, Thickness, antialiased: true);
    }

    private static void Shadow(Label label)
    {
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
    }
}
