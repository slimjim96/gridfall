using Gridfall.Core.Content;
using Gridfall.Core.Events;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 8. Bounties from this tick's deaths, lives from this tick's leaks.
///
/// Runs after damage so it sees this tick's deaths, not last tick's. Nothing
/// interesting happens here, which is the point.
/// </summary>
internal static class EconomySystem
{
    public static void Run(
        SimState state, ContentSet content, EventLog events, int tick,
        List<int> deadDefIndices, List<int> leakedDefIndices)
    {
        int goldDelta = 0;
        foreach (int defIndex in deadDefIndices)
            goldDelta += content.Enemy((ushort)defIndex).Bounty;

        if (goldDelta != 0)
        {
            state.Gold += goldDelta;
            events.Add(new SimEvent(tick, EventKind.GoldChanged, state.Gold, goldDelta));
        }

        int livesDelta = 0;
        foreach (int defIndex in leakedDefIndices)
            livesDelta -= content.Enemy((ushort)defIndex).LivesCost;

        if (livesDelta != 0)
        {
            bool wasAlive = state.Lives > 0;
            state.Lives += livesDelta;
            if (state.Lives < 0) state.Lives = 0;
            events.Add(new SimEvent(tick, EventKind.LivesChanged, state.Lives, livesDelta));

            // The sim reports the loss; it does not stop itself. Whether the run
            // ends is the caller's decision.
            if (wasAlive && state.Lives == 0)
                events.Add(new SimEvent(tick, EventKind.GameOver));
        }
    }
}
