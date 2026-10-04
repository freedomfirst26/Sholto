using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Performance;

/// <summary>See <see cref="IMagnetSnap"/>. Computed from jog recency, which is why it reads
/// <see cref="IJogRecencyReader"/>.</summary>
public sealed class MagnetSnap : IMagnetSnap
{
    private const double EngageThreshold = 0.3;     // same as glow threshold — see one, fire one
    private const double DisengageThreshold = 0.15; // hysteresis to avoid re-fire chatter

    private readonly IDecks _decks;
    private readonly IJogRecencyReader _recency;
    private readonly IFrameClock _clock;
    private readonly MagnetismOptions _options;
    private bool _lastEligible;
    private bool _quantizeFired;

    public MagnetSnap(IDecks decks, IJogRecencyReader recency, IFrameClock clock, IOptions<MagnetismOptions> options)
    {
        _decks = decks;
        _recency = recency;
        _clock = clock;
        _options = options.Value;
    }

    public event Action<bool>? EligibilityChanged;

    /// <summary>
    /// Magnet-lock eligibility. True iff both decks have completed basic analysis (BPM + beat grid), both
    /// are actually playing, neither is scratching, their <em>playback</em> BPMs (source x multiplier x tempo
    /// fader) are within <see cref="MagnetismOptions.BpmEligibilityTolerance"/>, and the user isn't rotating
    /// <em>both</em> jog wheels at once (a dual-jog gesture is deliberate; the magnet holds off).
    /// </summary>
    private bool IsBpmEligible()
    {
        var deck1 = _decks.Deck1;
        var deck2 = _decks.Deck2;
        if (!deck1.HasAnalysis || !deck2.HasAnalysis) return false;
        if (!deck1.Loading.IsPlaying || !deck2.Loading.IsPlaying) return false;
        // A scratching deck isn't a candidate for a magnetic beat-snap —
        // Quantize()'s SeekRelative would yank the platter out from under
        // the user's hand mid-gesture. (Also covers the force-Play() a
        // paused deck gets while scratched: without this gate that alone
        // could newly satisfy "both decks playing" and fire a surprise snap.)
        if (deck1.IsScratching || deck2.IsScratching) return false;

        double eff1 = deck1.EffectiveBpm;
        double eff2 = deck2.EffectiveBpm;
        if (eff1 <= 0 || eff2 <= 0) return false;

        double diff = Math.Abs(eff1 - eff2) / Math.Max(eff1, eff2);
        if (diff > _options.BpmEligibilityTolerance) return false;

        // Both decks being jogged simultaneously → user is in the middle of
        // a manual adjustment, don't surprise them with a lock.
        if (_recency.BothDecksActivelyJogging) return false;

        return true;
    }

    public double Factor()
    {
        if (!IsBpmEligible()) return 0;
        var deck1 = _decks.Deck1;
        var deck2 = _decks.Deck2;
        var d1 = deck1.Analysis.Basic?.DownbeatTimes;
        var d2 = deck2.Analysis.Basic?.DownbeatTimes;
        if (d1 is null || d1.Length == 0 || d2 is null || d2.Length == 0) return 0;

        double phase1 = deck1.PlaybackSeconds - deck1.NearestDownbeatSec();
        double phase2 = deck2.PlaybackSeconds - deck2.NearestDownbeatSec();
        double misalign = Math.Abs(phase1 - phase2);

        const double window = 0.15;  // 150 ms — bar-start tolerance is wider than beat-start
        double t = Math.Min(misalign / window, 1);
        return 1 - t * t * (3 - 2 * t);  // smoothstep, 1 at t=0 → 0 at t=1
    }

    public void Update()
    {
        // Publish the binary eligibility so the centerline magnet glyph
        // pops in/out via its own style-class transition.
        bool eligible = IsBpmEligible();
        if (eligible != _lastEligible)
        {
            _lastEligible = eligible;
            EligibilityChanged?.Invoke(eligible);
        }

        double f = Factor();

        if (f < DisengageThreshold)
            _quantizeFired = false;  // user pulled them apart; re-arm

        // Fire once: greens visible + user let go of the jog for a beat.
        // Crucial gate: ignore if the user hasn't jogged at all this session
        // (LastJogAt = DateTime.MinValue), or if their last jog was so long ago
        // that "the user just let go" isn't a believable framing any more. This
        // is what stops two decks running at different tempos from triggering a
        // surprise seek every time their phases drift into alignment.
        var sinceJog = _clock.Now - _recency.LastJogAt;
        bool userRecentlyReleasedJog =
            _recency.LastJogAt != DateTime.MinValue
            && sinceJog > _options.JogIdleForQuantize
            && sinceJog < _options.JogRecencyForQuantize;

        if (!_quantizeFired
            && f >= EngageThreshold
            && userRecentlyReleasedJog)
        {
            Quantize();
            _quantizeFired = true;
        }

        // Show greens whenever engaged AND we haven't snapped yet. Once snapped, the
        // visuals collapse — that's the "locked, hands off" signal.
        bool active = f >= EngageThreshold && !_quantizeFired;
        _decks.Deck1.MagneticGlowSec = active ? _decks.Deck1.NearestDownbeatSec() : -1;
        _decks.Deck2.MagneticGlowSec = active ? _decks.Deck2.NearestDownbeatSec() : -1;
    }

    /// <summary>Snap the last-jogged deck to the reference deck — phase aligns
    /// the downbeats AND tempo-locks so the link actually holds. Without the
    /// tempo lock, a fraction-of-a-percent BPM difference (e.g. 176.5 vs 176.6)
    /// would let the decks drift apart immediately after the snap.</summary>
    private void Quantize()
    {
        var deck1 = _decks.Deck1;
        var deck2 = _decks.Deck2;
        IDeckSession adjusted, reference;
        if (_recency.LastJoggedDeck == 1 || _recency.LastJoggedDeck == 2)
        {
            adjusted  = _recency.LastJoggedDeck == 1 ? deck1 : deck2;
            reference = _recency.LastJoggedDeck == 1 ? deck2 : deck1;
        }
        else
        {
            // No jog history — pick whichever deck is further from its own downbeat
            // (the one with more error to correct).
            double e1 = Math.Abs(deck1.PlaybackSeconds - deck1.NearestDownbeatSec());
            double e2 = Math.Abs(deck2.PlaybackSeconds - deck2.NearestDownbeatSec());
            (adjusted, reference) = e1 > e2 ? (deck1, deck2) : (deck2, deck1);
        }

        // 1) Tempo-lock: pull the adjusted deck's EffectiveBpm onto the reference.
        //    Done first so the phase math below works against the locked tempo.
        adjusted.MatchEffectiveBpm(reference.EffectiveBpm);

        // 2) Phase-snap: shift adjusted so its next downbeat lands at the same
        //    wall-clock moment as the reference's next downbeat. Math: place
        //    adj at (its nearest downbeat) + (ref's offset past *its* nearest
        //    downbeat). Walks the same number of seconds past a downbeat as
        //    ref, so the next beats fire together — independent of which bar
        //    of either song they happen to be in.
        double refPhase = reference.PlaybackSeconds - reference.NearestDownbeatSec();
        double adjDownbeat = adjusted.NearestDownbeatSec();
        if (adjDownbeat < 0) return;
        double delta = (adjDownbeat + refPhase) - adjusted.PlaybackSeconds;
        if (Math.Abs(delta) > 0.0001) adjusted.Transport.SeekRelative(delta);
        Console.WriteLine($"[Magnet] snap: refPhase={refPhase:F4}s adjDownbeat={adjDownbeat:F4}s delta={delta:F4}s | refBpm={reference.EffectiveBpm:F3} adjBpm={adjusted.EffectiveBpm:F3}");
    }
}
