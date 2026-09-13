using System.Threading;
using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Per-deck beat-synced dub echo: a feedback delay line whose time locks to the
/// track's tempo (default 1/2 beat — classic dub slap-back). Toggling it OFF is
/// deliberately NOT a bypass — the existing tail keeps ringing out (feedback
/// keeps decaying, wet stays audible) while simply refusing new input into the
/// line, exactly like letting go of a physical echo-out button. Toggling ON
/// resumes feeding input in.
///
/// Modeled on <see cref="BiquadEq3Band"/>'s lock-free pattern: UI/MIDI threads
/// write target state via <see cref="SetEnabled"/> / <see cref="SetTempo"/>; the
/// audio thread reads it once per buffer. The delay line is a fixed-capacity
/// ring buffer allocated once at construction — no allocation in the hot path.
///
/// <see cref="SetTempo"/> is now called continuously while the echo runs —
/// <see cref="DeckMixer"/> re-derives BPM×speed on every tempo-fader move and
/// every analysis update and pushes it here (see DeckMixer.PushEchoTempo) —
/// not just once on enable. Design call: changing <c>_delaySamples</c> while a
/// tail is ringing moves the read cursor, which is a discontinuity in the
/// delayed signal — audibly a click, and a pitch/glitch artefact if the jump
/// is large. Taking the simple option here rather than building a
/// crossfading or resampling delay line, because the jumps in practice are
/// tiny: at 128 BPM a 0.1% tempo-fader nudge shifts a half-beat delay by
/// ~23 samples (well under a millisecond — inaudible). The one case that
/// isn't small is a <c>BpmMultiplier</c> half/double click (a full-beat jump
/// in the delay target), which will be audibly discontinuous; that's accepted
/// as a rare, momentary artefact rather than a reason to add a resampling
/// delay line — that would be a feature (smooth tempo-tracking delay), not a
/// fix for "echo ignores tempo changes".
/// </summary>
public sealed class EchoEffect : DeckEffect
{
    /// <inheritdoc/>
    public override string EffectId => "echo";

    private static readonly EffectParam[] ParamTable =
    [
        new(0, "Enabled", 0.0, 1.0, 0.0, EffectParamCurve.Boolean),
        new(1, "Beats",   0.125, 4.0, 0.5, EffectParamCurve.Linear),
    ];

    /// <inheritdoc/>
    public override ReadOnlySpan<EffectParam> Params => ParamTable;

    /// <inheritdoc/>
    public override void SetParam(int paramId, double value)
    {
        switch (paramId)
        {
            case 0: SetEnabled(value != 0); break;
            case 1: Beats = value; break;
        }
    }

    // Feedback delay character. Not user-tunable in v1 — see class doc.
    private const float Feedback = 0.55f;
    private const float Wet = 0.5f;
    private const float Dry = 1.0f;

    // Ring buffer sized generously (4 s per channel) so even a slow track's
    // half-beat delay fits comfortably — 4s covers half-beats down to 30 BPM.
    private const double CapacitySeconds = 4.0;

    private readonly int _sampleRate;
    private readonly int _capacityFrames;
    // One ring buffer per channel — keeps left/right delay fully independent,
    // so ProcessSample (called per-channel, no frame grouping) stays correct
    // without needing to know about the other channel.
    private readonly float[][] _delayLine;

    // Shared write cursor (index into every channel's ring buffer at once) —
    // valid as long as Process() advances it exactly once per frame, after
    // every channel in that frame has read+written. See ProcessSample's doc
    // for the (looser) guarantee it makes when called directly.
    private int _writePos;

    // Target delay length in samples — recomputed by SetTempo, read lock-free.
    private int _delaySamples;

    // Target on/off — read lock-free; ramped into _inputGain over ~5ms so a
    // toggle mid-buffer doesn't click.
    private int _enabled; // 0/1, Volatile-accessed (no bool Volatile overload)
    private float _inputGain;
    private readonly float _gainAlpha;

    // The bpm most recently passed to SetTempo (or the 128 default until the
    // first call) — cached so a Beats change alone can recompute the delay
    // without waiting for the echo to be re-enabled. See Beats below.
    private double _lastBpm = DeckMixer.DefaultBpm;
    private double _beats = 0.5;

    /// <summary>Echo repeat time, in fractions of a beat. 0.5 (the default) is
    /// the classic dub half-beat slap-back. Setting this recomputes the delay
    /// immediately from the last tempo passed to <see cref="SetTempo"/> —
    /// it used to take effect only on the next enable, since only SetTempo
    /// recomputed the delay length.</summary>
    public double Beats
    {
        get => _beats;
        set { _beats = value; RecomputeDelay(); }
    }

    public EchoEffect(SfEngine engine, AudioFormat format)
    {
        _ = engine;  // base is parameterless; param kept for API parity with the other modifiers
        _sampleRate = format.SampleRate;
        int channels = Math.Max(1, format.Channels);

        _capacityFrames = (int)(_sampleRate * CapacitySeconds);
        _delayLine = new float[channels][];
        for (int c = 0; c < channels; c++) _delayLine[c] = new float[_capacityFrames];

        // ~5 ms one-pole ramp for the input-feed gain (same shape as
        // BiquadEq3Band's GainSmoothAlpha, just tau-matched to the spec's
        // "ramp over ~5ms on toggle" instead of that class's slower knob feel).
        _gainAlpha = TempoMath.FiveMsGainAlpha(_sampleRate);

        SetTempo(DeckMixer.DefaultBpm); // sane default so a delay exists even before the first enable
    }

    /// <summary>Feed the delay line (ON) or let it ring out untouched (OFF).
    /// Safe to call from any thread.</summary>
    public void SetEnabled(bool on) => Volatile.Write(ref _enabled, on ? 1 : 0);

    /// <summary>Recompute the delay time from a BPM: <see cref="Beats"/> ×
    /// (60/bpm) × sample rate, clamped to the ring buffer's capacity. Called
    /// on enable and then continuously while the echo runs, from every
    /// tempo-fader move and every analysis update (see
    /// <see cref="DeckMixer"/>) — see the class doc for the accepted
    /// discontinuity this causes when the target moves while a tail is
    /// ringing. Safe to call from any thread.</summary>
    public void SetTempo(double bpm)
    {
        if (bpm <= 0) bpm = DeckMixer.DefaultBpm;
        _lastBpm = bpm;
        RecomputeDelay();
    }

    private void RecomputeDelay()
    {
        int samples = TempoMath.BeatsToSamples(_beats, _lastBpm, _sampleRate, _capacityFrames);
        Volatile.Write(ref _delaySamples, samples);
    }

    /// <summary>Disable the echo and flush its delay line — called by
    /// <see cref="Deck.ResetControls"/> on track load so the previous track's
    /// tail can't ring into the new one. Does not reallocate: it zeroes the
    /// existing ring buffers and resets the write cursor / input-gain ramp to
    /// their startup values. Called from the UI thread while the audio thread
    /// may concurrently be inside <see cref="Process"/> — same lock-free
    /// tolerance as every other control write in this class (worst case one
    /// buffer reads a half-cleared line, never a torn sample).</summary>
    public void Reset()
    {
        SetEnabled(false);
        _inputGain = 0f;
        _writePos = 0;
        foreach (var line in _delayLine) Array.Clear(line);
    }

    public override void Process(Span<float> buffer, int channels)
    {
        bool on = Volatile.Read(ref _enabled) != 0;
        int delaySamples = Volatile.Read(ref _delaySamples);
        float targetGain = on ? 1f : 0f;
        float gain = _inputGain;
        int lineCount = _delayLine.Length;
        int capacity = _capacityFrames;
        int writePos = _writePos;

        int frames = buffer.Length / channels;
        for (int i = 0; i < frames; i++)
        {
            gain += (targetGain - gain) * _gainAlpha;

            int readPos = writePos - delaySamples;
            if (readPos < 0) readPos += capacity;

            int baseIdx = i * channels;
            int chCount = Math.Min(channels, lineCount);
            for (int ch = 0; ch < chCount; ch++)
            {
                var line = _delayLine[ch];
                float input = buffer[baseIdx + ch];
                float delayed = line[readPos];

                // Feedback continues regardless of `on` — that's the tail-out
                // behaviour. Only the freshly-fed input is gated by `gain`,
                // which is what stops NEW energy entering the line when off.
                line[writePos] = input * gain + delayed * Feedback;
                buffer[baseIdx + ch] = input * Dry + delayed * Wet;
            }

            writePos++;
            if (writePos >= capacity) writePos = 0;
        }

        _writePos = writePos;
        _inputGain = gain;
    }

    public override float ProcessSample(float sample, int channel)
    {
        // Single-sample path: not the hot path (Process handles real
        // playback), so gain ramping and the write-cursor advance happen
        // per call here rather than once per frame. Correct for a single
        // channel; if called for multiple channels of the same frame the
        // cursor advances once per call instead of once per frame, which
        // slightly desyncs L/R phase — acceptable since nothing in this
        // codebase drives modifiers through ProcessSample today.
        if ((uint)channel >= (uint)_delayLine.Length) return sample;

        bool on = Volatile.Read(ref _enabled) != 0;
        int delaySamples = Volatile.Read(ref _delaySamples);
        float targetGain = on ? 1f : 0f;
        _inputGain += (targetGain - _inputGain) * _gainAlpha;

        var line = _delayLine[channel];
        int readPos = _writePos - delaySamples;
        if (readPos < 0) readPos += _capacityFrames;
        float delayed = line[readPos];

        line[_writePos] = sample * _inputGain + delayed * Feedback;
        float output = sample * Dry + delayed * Wet;

        _writePos++;
        if (_writePos >= _capacityFrames) _writePos = 0;
        return output;
    }
}
