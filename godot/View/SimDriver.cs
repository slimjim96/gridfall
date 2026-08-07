using System.Collections.Generic;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;

namespace Gridfall.View;

/// <summary>
/// Owns the Sim and advances it on a fixed-timestep accumulator -- never on
/// frame delta. A variable timestep is not a timestep (engine guide 02).
///
/// The renderer interpolates between ticks using Alpha. That value is view-side
/// only and never re-enters Core.
/// </summary>
public sealed class SimDriver
{
    public const float TickSeconds = 1.0f / Sim.TicksPerSecond;

    /// <summary>
    /// A long stall must not spiral: catching up 200 ticks in one frame would
    /// stall again and compound. Past the cap we drop game time instead, which
    /// is a view-side decision the sim cannot detect.
    /// </summary>
    private const int MaxCatchUpTicks = 5;

    private readonly Sim _sim;
    private float _accumulator;

    /// <summary>
    /// Events for the frame, accumulated across every tick the frame ran.
    ///
    /// Drained inside the catch-up loop, not after it: draining afterwards
    /// loses a tick's events whenever two ticks run in one frame, and that only
    /// shows up under stutter -- which is exactly when it matters.
    /// </summary>
    public readonly List<SimEvent> FrameEvents = new(64);

    public SimDriver(MapDef map, ContentSet content, uint seed)
    {
        _sim = new Sim(map, content, seed);
    }

    public Sim Sim => _sim;
    public SimStateView State => _sim.State;
    public MapDef Map => _sim.Map;
    public ContentSet Content => _sim.Content;
    public int TickCount => _sim.TickCount;

    /// <summary>How far between the last tick and the next, in [0,1). Rendering only.</summary>
    public float Alpha { get; private set; }

    /// <summary>True on frames where at least one tick ran.</summary>
    public bool Ticked { get; private set; }

    public void Enqueue(ICommand command) => _sim.Enqueue(command);

    public void Advance(float delta)
    {
        FrameEvents.Clear();
        Ticked = false;

        _accumulator += delta;

        int ticks = 0;
        while (_accumulator >= TickSeconds && ticks < MaxCatchUpTicks)
        {
            _sim.Tick();
            foreach (SimEvent e in _sim.Events.Span) FrameEvents.Add(e);

            _accumulator -= TickSeconds;
            ticks++;
            Ticked = true;
        }

        if (ticks == MaxCatchUpTicks)
        {
            // Drop the backlog rather than chase it.
            _accumulator = 0f;
        }

        Alpha = _accumulator / TickSeconds;
    }

    /// <summary>Advance exactly one tick, ignoring the clock. For deterministic tests.</summary>
    public void StepOneTick()
    {
        FrameEvents.Clear();
        _sim.Tick();
        foreach (SimEvent e in _sim.Events.Span) FrameEvents.Add(e);
        Ticked = true;
        Alpha = 0f;
    }
}
