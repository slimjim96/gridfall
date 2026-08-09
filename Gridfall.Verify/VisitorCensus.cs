using Gridfall.Core.Content;

namespace Gridfall.Verify;

/// <summary>
/// What the player has already fought, and therefore what a station is worth to
/// them per gold.
///
/// This exists because <c>fussiness</c> is subtracted PER HIT. A station's value
/// is not a property of the station -- it is a property of the station against a
/// mix of visitors, and the ordering flips inside the shipped roster:
///
/// <code>
///   serving per SECOND per gold      arrow          cannon
///   (12 every 0.6s for 50g)     (40 every 1.5s for 90g)
///   fussiness 0                      0.400           0.296
///   fussiness 4                      0.267           0.267   &lt;- crossover
///   fussiness 8 (husk)               0.133           0.237
/// </code>
///
/// Anything that ranks stations on BASE serving therefore describes a game with
/// one station in it. The harness did exactly that, so no measured run in this
/// repo had ever built a cannon.
///
/// The weights come only from waves that have already STARTED. That boundary is
/// the whole honesty of this class: the game shows no wave preview -- the HUD
/// prints "wave N incoming" and nothing about its composition -- so a policy
/// weighting against the NEXT wave would be reading the wave table, which is a
/// thing no player can do. Weighting against waves already met is memory, which
/// every player has.
/// </summary>
public sealed class VisitorCensus
{
    /// <summary>
    /// Fixed-point denominator for the weighted average. Integer, so two runs on
    /// two machines cannot disagree about a station's rank -- the harness is
    /// outside the simulation, but a policy that decided differently under a
    /// different rounding would make every balance figure machine-specific.
    /// </summary>
    public const int Scale = 1024;

    private readonly ContentSet _content;
    private readonly long[] _weightByDef;

    private long _total;
    private int _wavesCounted;

    public VisitorCensus(ContentSet content)
    {
        _content = content;
        _weightByDef = new long[content.Visitors.Length];
    }

    /// <summary>Waves folded in so far. The census is empty before wave 1.</summary>
    public int WavesCounted => _wavesCounted;

    public bool IsEmpty => _total == 0;

    /// <summary>
    /// Fold in every wave up to <paramref name="wavesStarted"/> that is not in
    /// already. <c>SimState.WaveIndex</c> is the count of waves STARTED, so
    /// passing it mid-wave includes the wave being fought -- correct, because the
    /// player can see it on the board.
    ///
    /// Idempotent: safe to call every tick, which is what the policy does.
    /// </summary>
    public void ObserveWavesStarted(int wavesStarted)
    {
        int upto = System.Math.Min(wavesStarted, _content.Waves.Length);
        for (; _wavesCounted < upto; _wavesCounted++)
        {
            foreach (WaveEntry entry in _content.Waves[_wavesCounted].Entries)
            {
                // Weighted by APPETITE, not by head count: a station is bought to
                // chew through health, and forty mites are not the problem two
                // brutes are. Base appetite rather than the wave's scaled value --
                // AppetiteScale multiplies every archetype in a wave equally, so
                // it cannot change which station wins, only how loudly late waves
                // shout over early ones.
                _weightByDef[entry.VisitorIndex] += (long)entry.Count * _content.Visitor(entry.VisitorIndex).Appetite;
                _total += (long)entry.Count * _content.Visitor(entry.VisitorIndex).Appetite;
            }
        }
    }

    /// <summary>
    /// Serving a single hit of <paramref name="serving"/> actually lands, averaged
    /// over the census and multiplied by <see cref="Scale"/>.
    ///
    /// An empty census returns the unreduced figure -- before wave 1 the player
    /// has met nothing, and guessing would be a worse model than not knowing.
    /// </summary>
    public long EffectiveServingScaled(int serving)
    {
        if (_total == 0) return (long)serving * Scale;

        long weighted = 0;
        for (ushort v = 0; v < _weightByDef.Length; v++)
        {
            if (_weightByDef[v] == 0) continue;
            weighted += _weightByDef[v] * _content.Visitor(v).ServingTaken(serving);
        }
        return weighted * Scale / _total;
    }

    /// <summary>
    /// Serving per tick per gold, scaled to stay in integers, against this census.
    /// Zero for a station that cannot serve -- the caller filters on affordability,
    /// not on this.
    /// </summary>
    public long ValuePerGold(StationDef def)
    {
        if (def.Serving <= 0 || def.CooldownTicks <= 0 || def.Cost <= 0) return 0;
        return EffectiveServingScaled(def.Serving) * 1000 / (def.CooldownTicks * (long)def.Cost);
    }

    /// <summary>
    /// The same figure as a double, for the reports. Verify is not Core and may
    /// use floating point (ADR-0002 binds the simulation, not the harness) -- but
    /// nothing that feeds a COMMAND may, which is why the policy takes the
    /// integer form above.
    /// </summary>
    public double ServingPerTickPerGold(StationDef def)
        => def.Serving <= 0 || def.CooldownTicks <= 0 || def.Cost <= 0
            ? 0
            : EffectiveServingScaled(def.Serving) / (double)Scale / def.CooldownTicks / def.Cost;

    /// <summary>A census over one wave only, for per-wave analysis.</summary>
    public static VisitorCensus ForWave(ContentSet content, int waveIndexZeroBased)
    {
        var census = new VisitorCensus(content);
        census._wavesCounted = waveIndexZeroBased;
        census.ObserveWavesStarted(waveIndexZeroBased + 1);
        return census;
    }
}
