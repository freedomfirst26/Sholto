namespace Sholto.Audio;

/// <summary>
/// Beat loops, extracted out of <see cref="Deck"/>. v1 only supports the stem
/// path — the chunked / raw provider paths log and no-op until wrap support
/// lands there too (unchanged from the pre-extraction behaviour).
///
/// Loops are read on the audio thread (via the stem provider's own lock-free
/// state), but every member here runs on the UI/MIDI thread that mutates them —
/// nothing here is on the audio callback path, so the accessor-delegate
/// indirection below costs nothing that wasn't already being paid as a field
/// read on <c>Deck</c>.
///
/// Deck owns the data these methods read (the live stem provider, the current
/// analysis, playback position, sample count) because all of it is also read
/// by Deck's other components (playback, beatgrid). Rather than back-reference
/// Deck itself — which would let this class reach into arbitrary Deck state —
/// Deck hands in narrow accessor delegates for exactly what loop logic needs.
/// </summary>
internal sealed class DeckLooping : IDeckLooping
{
    /// <summary>Shortest allowed loop, in interleaved samples. Below this the
    /// wrap-crossfade tail has nowhere to fit.</summary>
    private const long MinLoopLengthSamples = 64;

    private readonly Func<StemMixDataProvider?> _stemProvider;
    private readonly Func<TrackAnalysis> _analysis;
    private readonly Func<long> _positionFrames;
    private readonly Func<long> _sampleCount;

    public DeckLooping(
        Func<StemMixDataProvider?> stemProvider,
        Func<TrackAnalysis> analysis,
        Func<long> positionFrames,
        Func<long> sampleCount)
    {
        _stemProvider = stemProvider;
        _analysis = analysis;
        _positionFrames = positionFrames;
        _sampleCount = sampleCount;
    }

    private LoopRegion? _activeLoop;

    /// <inheritdoc/>
    public LoopRegion? ActiveLoop => _activeLoop;

    /// <inheritdoc/>
    public event Action<LoopRegion?>? LoopChanged;

    /// <summary>Engage an N-bar auto-loop snapped to the nearest downbeat. If
    /// a loop is already active this toggles it off (matches the FLX-4's 4
    /// BEAT button's "press again to exit" behaviour). No-op if the beatgrid
    /// hasn't landed yet.
    /// <para>The button is labelled "4 BEAT" on the controller but in practice
    /// — confirmed with the user's workflow — we treat the parameter as BARS,
    /// not beats. Default 4 = a 4-bar loop, which is the musically useful
    /// scale for DJing (it's a phrase, not just a beat).</para></summary>
    public void EnableBeatLoop(int bars)
    {
        if (_activeLoop is not null) { ExitLoop(); return; }
        var stemProvider = _stemProvider();
        if (stemProvider is null) { Console.WriteLine("[Deck] loop: stems not loaded — ignored"); return; }
        var downbeats = _analysis().Basic?.DownbeatTimes;
        if (downbeats is null || downbeats.Length < 2)
        {
            Console.WriteLine("[Deck] loop: downbeat grid not ready — ignored");
            return;
        }

        double bpm = _analysis().Basic?.Bpm ?? 0;
        if (bpm <= 0)
        {
            Console.WriteLine("[Deck] loop: BPM unknown — ignored");
            return;
        }

        double nowSec = _positionFrames() / (double)AudioFileDecoder.TargetSampleRate;
        double secondsPerBar = 60.0 / bpm * 4.0;
        var beats = _analysis().Basic?.BeatTimes;

        // Loop-in / period strategy. Default is "beat-snap" — A/B testing
        // showed madmom's individual beat positions are reliable but its
        // downbeat (1-of-4) phase labeling drifts on some tracks (loop
        // landed on beat 2 or 4 instead of beat 1, audibly wrong rhythm).
        // Beat-snap sidesteps this by treating any beat as a valid loop-in
        // and deriving the period from N×4 consecutive beats — sample-exact
        // to madmom's beat grid, with no dependence on downbeat phase.
        //
        // SHOLTO_LOOP_MODE values:
        //   beat-snap  (DEFAULT) loop-in = nearest beat; period = beatTimes[bidx+bars*4] − beatTimes[bidx]
        //                        Falls back to downbeat-grid if BeatTimes is empty.
        //   downbeat             loop-in = nearest downbeat; period = downbeats[idx+bars]
        //                        The old default — kept for A/B testing.
        //   bpm                  loop-in = nearest downbeat; period = bars × 60/bpm × 4
        //   no-snap / tap        loop-in = current play position; period = bars × 60/bpm × 4
        var mode = Environment.GetEnvironmentVariable("SHOLTO_LOOP_MODE") ?? "beat-snap";
        double inSec, outSec;
        string source;
        switch (mode)
        {
            case "downbeat":
            {
                int inIdx = NearestBeatIndex(downbeats, nowSec);
                inSec = downbeats[inIdx];
                int outIdx = inIdx + bars;
                outSec = outIdx < downbeats.Length
                    ? downbeats[outIdx]
                    : inSec + bars * secondsPerBar;
                source = outIdx < downbeats.Length ? "downbeat-grid" : "downbeat-in/BPM-fallback";
                break;
            }
            case "bpm":
            {
                int inIdx = NearestBeatIndex(downbeats, nowSec);
                inSec = downbeats[inIdx];
                outSec = inSec + bars * secondsPerBar;
                source = "downbeat-in/BPM-period";
                break;
            }
            case "no-snap":
            case "tap":
            {
                inSec = nowSec;
                outSec = inSec + bars * secondsPerBar;
                source = "no-snap/BPM-period";
                break;
            }
            case "beat-snap":
            default:
            {
                if (beats is not null && beats.Length >= bars * 4 + 1)
                {
                    int bIdx = NearestBeatIndex(beats, nowSec);
                    inSec = beats[bIdx];
                    int outBIdx = bIdx + bars * 4;
                    outSec = outBIdx < beats.Length
                        ? beats[outBIdx]
                        : inSec + bars * secondsPerBar;
                    source = outBIdx < beats.Length ? "beat-in/beatgrid" : "beat-in/BPM-fallback";
                }
                else
                {
                    // BeatTimes empty / short — usually means an old analysis
                    // record from before beat-level detection was wired in.
                    // Fall back to the downbeat grid so the loop still works.
                    int inIdx = NearestBeatIndex(downbeats, nowSec);
                    inSec = downbeats[inIdx];
                    int outIdx = inIdx + bars;
                    outSec = outIdx < downbeats.Length
                        ? downbeats[outIdx]
                        : inSec + bars * secondsPerBar;
                    source = outIdx < downbeats.Length ? "downbeat-grid-fallback" : "downbeat-in/BPM-fallback";
                }
                break;
            }
        }

        long inSample  = SecondsToInterleavedSample(inSec);
        long outSample = SecondsToInterleavedSample(outSec);

        long maxSample = _sampleCount() * 2;
        if (outSample > maxSample) outSample = maxSample;
        if (outSample - inSample < MinLoopLengthSamples) // sanity floor
        {
            Console.WriteLine("[Deck] loop: computed range too small — ignored");
            return;
        }

        // Build the wrap-crossfade tail BEFORE publishing the loop region, so
        // the audio thread can't see a loop active without a tail to fade
        // with. (SetLoop's Volatile ordering writes loopEnd last; this
        // BuildLoopTail call's Volatile.Write completes happens-before.)
        stemProvider.BuildLoopTail(outSample);

        var region = new LoopRegion(inSample, outSample);
        _activeLoop = region;
        stemProvider.SetLoop(region);
        double actualLen = (outSample - inSample) / 2.0 / AudioFileDecoder.TargetSampleRate;
        double expectedLen = bars * secondsPerBar;
        Console.WriteLine($"[Deck] loop ON [{mode}]: {bars} bars @ {bpm:F1} BPM, {inSec:F3}s → {outSec:F3}s, length {actualLen:F3}s (expected {expectedLen:F3}s, source: {source})");
        LoopChanged?.Invoke(region);
    }

    /// <summary>Halve the active loop's length (loop-in stays, loop-out moves).
    /// Floor at 64 samples. No-op if not looping.</summary>
    public void HalveLoop()
    {
        var stemProvider = _stemProvider();
        if (_activeLoop is null || stemProvider is null) return;
        var r = _activeLoop.Value;
        long newLen = r.LengthSamples / 2;
        if (newLen < MinLoopLengthSamples) { Console.WriteLine("[Deck] loop: at minimum length"); return; }
        // Keep loop-out frame-aligned (stereo, so even).
        newLen &= ~1L;
        var next = new LoopRegion(r.StartSample, r.StartSample + newLen);
        _activeLoop = next;
        stemProvider.BuildLoopTail(next.EndSample);
        stemProvider.SetLoop(next);
        Console.WriteLine($"[Deck] loop ½×: now {next.LengthSamples} samples");
        LoopChanged?.Invoke(next);
    }

    /// <summary>Double the active loop's length, clamped to track end. No-op if
    /// not looping.</summary>
    public void DoubleLoop()
    {
        var stemProvider = _stemProvider();
        if (_activeLoop is null || stemProvider is null) return;
        var r = _activeLoop.Value;
        long newLen = r.LengthSamples * 2;
        long maxSample = _sampleCount() * 2;
        long newOut = r.StartSample + newLen;
        if (newOut > maxSample) newOut = maxSample;
        if (newOut <= r.StartSample) return;
        var next = new LoopRegion(r.StartSample, newOut & ~1L);
        _activeLoop = next;
        stemProvider.BuildLoopTail(next.EndSample);
        stemProvider.SetLoop(next);
        Console.WriteLine($"[Deck] loop 2×: now {next.LengthSamples} samples");
        LoopChanged?.Invoke(next);
    }

    /// <summary>Exit the active loop; playback continues forward from the
    /// current cursor (no snap-back to loop-in). No-op if not looping.
    /// Also used by <c>Deck.ResetControls</c> to clear any active loop on a
    /// fresh track — same semantics, so it just calls this.</summary>
    public void ExitLoop()
    {
        if (_activeLoop is null) return;
        _activeLoop = null;
        _stemProvider()?.SetLoop(null);
        Console.WriteLine("[Deck] loop OFF");
        LoopChanged?.Invoke(null);
    }

    /// <summary>Shift an active loop by <paramref name="seconds"/> so it tracks
    /// a beatgrid phase nudge. Out-of-bounds shifts are dropped (loop stays
    /// put). Not on <see cref="IDeckLooping"/> — called only by
    /// <see cref="DeckBeatgrid"/>, a sibling component, via a delegate Deck
    /// wires at construction (see Deck's constructor).</summary>
    internal void ShiftLoop(double seconds)
    {
        var stemProvider = _stemProvider();
        if (_activeLoop is null || stemProvider is null) return;
        var current = _activeLoop.Value;
        long shiftSamples = ((long)Math.Round(seconds * AudioFileDecoder.TargetSampleRate) * 2) & ~1L;
        long newIn  = current.StartSample + shiftSamples;
        long newOut = current.EndSample   + shiftSamples;
        long maxSample = _sampleCount() * 2;
        if (newIn < 0 || newOut > maxSample) return;
        stemProvider.BuildLoopTail(newOut);
        var nextRegion = new LoopRegion(newIn, newOut);
        _activeLoop = nextRegion;
        stemProvider.SetLoop(nextRegion);
        LoopChanged?.Invoke(nextRegion);
    }

    private static long SecondsToInterleavedSample(double sec)
    {
        long s = (long)Math.Round(sec * AudioFileDecoder.TargetSampleRate) * 2;
        return s & ~1L;
    }

    private static int NearestBeatIndex(double[] beatTimes, double pos)
    {
        // Linear scan is fine — beat counts are O(few hundred). Binary search
        // would shave microseconds nobody will feel.
        int best = 0;
        double bestDelta = Math.Abs(beatTimes[0] - pos);
        for (int i = 1; i < beatTimes.Length; i++)
        {
            double d = Math.Abs(beatTimes[i] - pos);
            if (d < bestDelta) { best = i; bestDelta = d; }
        }
        return best;
    }
}
