using Gridfall.Core.Content;
using Gridfall.Core.Events;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 8. Bounties from this tick's deaths, patience from this tick's leaks.
///
/// Runs after serving so it sees this tick's deaths, not last tick's. Nothing
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
            goldDelta += content.Visitor((ushort)defIndex).Bounty;

        if (goldDelta != 0)
        {
            state.Gold += goldDelta;
            events.Add(new SimEvent(tick, EventKind.GoldChanged, state.Gold, goldDelta));
        }

        int patienceDelta = 0;
        foreach (int defIndex in leakedDefIndices)
            patienceDelta -= content.Visitor((ushort)defIndex).PatienceCost;

        if (patienceDelta != 0)
        {
            bool wasAlive = state.Patience > 0;
            state.Patience += patienceDelta;
            if (state.Patience < 0) state.Patience = 0;
            events.Add(new SimEvent(tick, EventKind.PatienceChanged, state.Patience, patienceDelta));

            // The sim reports the loss; it does not stop itself. Whether the run
            // ends is the caller's decision.
            if (wasAlive && state.Patience == 0)
                events.Add(new SimEvent(tick, EventKind.GameOver));
        }
    }
}
