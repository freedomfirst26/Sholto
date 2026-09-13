using System.Diagnostics;
using System.Threading;
using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Top-level 4-channel output component. Replaces "add each deck to the master
/// mixer": instead this single component is the device's source and it PULLS
/// each deck's post-EQ stereo (via the public <see cref="SoundComponent.Process"/>)
/// once per buffer, then composes the device output:
///
///   ch1-2 (master)     = Σ deck.postEq × deck.MasterGain   (channel × crossfade)
///   ch3-4 (headphones) = Σ deck.postEq × (deck.CueActive ? 1 : 0)   (PFL — pre-fader)
///
/// The cue mix is written only when the device has ≥4 channels (the FLX4). On a
/// 2-channel device it degrades to master-only, exactly as before.
///
/// Why pull instead of graph fan-out: SoundFlow sums components, it does not let
/// one component's output land on specific output channels. Pulling each deck
/// here gives per-deck buffers we can place on master vs cue independently, and
/// the fader/crossfader now live on the master path only so the cue tap is
/// genuinely pre-fader.
/// </summary>
internal sealed class CueOutputRouter : SoundComponent
{
    private readonly IReadOnlyList<IMixSource> _decks;
    private float[] _scratch = [];

    /// <summary>CALLBACK TIMING HISTOGRAM: measures how long <see cref="GenerateAudio"/>
    /// actually takes, bucketed against the real deadline (48 kHz, 20 ms period × 3
    /// buffers ≈ 20 ms average budget, ~40 ms of one-shot slack before an audible
    /// dropout — see AudioEngine.Start). Gated behind the SHOLTO_AUDIO_TIMING env var
    /// (checked once, at construction, off the audio path) so it costs nothing when
    /// off: no Stopwatch, no bucket array, and the per-call check is a single readonly
    /// bool test.
    ///
    /// Everything touched from the audio thread is pre-allocated in the constructor.
    /// No LINQ, no lists, no boxing, no string formatting, no locks, no allocation in
    /// GenerateAudio or RecordTiming. Buckets are updated with Interlocked so a
    /// concurrent snapshot read from another thread never tears; GetTimingSnapshot
    /// allocates its copy array, but only on the calling (non-audio) thread, on
    /// demand.
    ///
    /// Boundaries (ms) are clustered around the 20 ms deadline and out past the 40 ms
    /// dropout point, not spread linearly — that's the region where a single slow
    /// callback becomes an audible glitch, so it's the region worth resolving finely.
    /// The bucket at index i holds callbacks with duration &lt; TimingBucketBoundsMs[i]
    /// (and &gt;= the previous bound); the last (overflow) bucket holds everything at
    /// or past the final bound, i.e. the "you just glitched" bucket.</summary>
    private static readonly double[] TimingBoundsMs = [5, 10, 15, 18, 20, 22, 25, 30, 35, 40];

    private static readonly bool TimingEnabled =
        Environment.GetEnvironmentVariable("SHOLTO_AUDIO_TIMING") == "1";

    private readonly Stopwatch? _timingStopwatch;
    private readonly long[]? _timingBuckets;

    /// <summary>MASTER CUE: when true the post-fader master mix is also summed
    /// into the headphone cue (ch3-4), so you can monitor what's going to the
    /// speakers in your phones. Written from the UI thread, read once per buffer
    /// on the audio thread — a torn bool isn't possible and a one-buffer delay
    /// is inaudible, so plain volatile suffices (same contract as Deck.CueActive).
    /// Initial value comes in via the constructor (see AudioEngine.Start, which
    /// rebuilds this router on every device switch and re-applies its own
    /// remembered value); AudioEngine.SetMasterCue also writes here directly
    /// while a router is live, so a toggle takes effect immediately.</summary>
    public volatile bool MasterCueActive;

    public CueOutputRouter(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks, bool masterCueActive = false)
        : base(engine, format)
    {
        _decks = decks;
        MasterCueActive = masterCueActive;
        if (TimingEnabled)
        {
            _timingStopwatch = new Stopwatch();
            _timingBuckets = new long[TimingBoundsMs.Length + 1]; // +1 overflow bucket
        }
    }

    public override string Name { get; set; } = "CueOutputRouter";

    /// <summary>Read-only snapshot of the timing bucket boundaries, in milliseconds,
    /// paired with <see cref="GetTimingSnapshot"/>'s bucket order. Null when timing
    /// is not enabled (SHOLTO_AUDIO_TIMING != "1").</summary>
    public static IReadOnlyList<double>? TimingBucketBoundsMs => TimingEnabled ? TimingBoundsMs : null;

    /// <summary>Snapshot copy of the callback-timing histogram, safe to call from any
    /// thread (e.g. a UI timer or diagnostics view) without blocking or perturbing the
    /// audio thread. Returns null when timing is disabled. The allocation here is on
    /// the calling thread, on demand — never on the audio path.</summary>
    public long[]? GetTimingSnapshot()
    {
        var buckets = _timingBuckets;
        if (buckets is null) return null;
        var snapshot = new long[buckets.Length];
        for (int i = 0; i < buckets.Length; i++)
        {
            snapshot[i] = Interlocked.Read(ref buckets[i]);
        }
        return snapshot;
    }

    protected override void GenerateAudio(Span<float> buffer, int channels)
    {
        if (TimingEnabled) _timingStopwatch!.Restart();

        buffer.Clear();
        if (channels <= 0)
        {
            RecordTiming();
            return;
        }

        int frames = buffer.Length / channels;
        int need = frames * 2; // deck output is stereo
        if (_scratch.Length < need) _scratch = new float[need];
        var stereo = _scratch.AsSpan(0, need);
        bool hasCue = channels >= 4;
        bool masterCue = MasterCueActive;   // read once per buffer

        foreach (var deck in _decks)
        {
            // Pull this deck's post-EQ, full-level stereo (fader NOT applied —
            // the SoundPlayer runs at unity; MasterGain is applied below).
            stereo.Clear();
            deck.Component.Process(stereo, 2);

            float mg = deck.MasterGain;           // channel × crossfade
            float cg = deck.CueActive ? 1f : 0f;  // PFL: full level, pre-fader
            // MASTER CUE: also fold this deck's post-fader master contribution
            // into the phones, so the headphone bus carries the master mix. A
            // faded-down, un-cued deck (mg==0) still adds nothing — correct.
            if (masterCue) cg += mg;
            if (mg == 0f && cg == 0f) continue;

            MixDeckInto(buffer, stereo, frames, channels, mg, cg);
        }

        RecordTiming();
    }

    /// <summary>Buckets the current GenerateAudio call's elapsed time. No allocation,
    /// no locks: a linear scan of a small fixed array and an Interlocked.Increment.
    /// No-op (branch-and-return) when timing is disabled.</summary>
    private void RecordTiming()
    {
        if (!TimingEnabled) return;

        double elapsedMs = _timingStopwatch!.Elapsed.TotalMilliseconds;
        double[] bounds = TimingBoundsMs;
        int idx = bounds.Length; // default: overflow bucket (>= last bound)
        for (int i = 0; i < bounds.Length; i++)
        {
            if (elapsedMs < bounds[i])
            {
                idx = i;
                break;
            }
        }

        Interlocked.Increment(ref _timingBuckets![idx]);
    }

    /// <summary>Accumulate one deck's stereo into the interleaved device buffer:
    /// master (× <paramref name="masterGain"/>) on ch1-2, and — when the device
    /// has ≥4 channels — the pre-fader cue (× <paramref name="cueGain"/>) on
    /// ch3-4. Cue uses <paramref name="cueGain"/> only (never the fader), so a
    /// cued deck with its fader down is silent on master yet full on the cue.</summary>
    internal static void MixDeckInto(
        Span<float> buffer, ReadOnlySpan<float> stereo, int frames, int channels,
        float masterGain, float cueGain)
    {
        bool hasCue = channels >= 4;
        for (int f = 0; f < frames; f++)
        {
            float l = stereo[f * 2];
            float r = stereo[f * 2 + 1];
            int o = f * channels;
            buffer[o]     += l * masterGain; // master L (ch1)
            buffer[o + 1] += r * masterGain; // master R (ch2)
            if (hasCue)
            {
                buffer[o + 2] += l * cueGain; // cue L (ch3) → headphones
                buffer[o + 3] += r * cueGain; // cue R (ch4) → headphones
            }
        }
    }
}
