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
/// </summary>
public sealed class EchoEffect : SoundModifier
{
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

    /// <summary>Echo repeat time, in fractions of a beat. 0.5 (the default) is
    /// the classic dub half-beat slap-back. Read by <see cref="SetTempo"/> —
    /// set this before enabling if a different subdivision is ever wanted.</summary>
    public double Beats { get; set; } = 0.5;

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
        _gainAlpha = 1f - MathF.Exp(-1f / (0.005f * _sampleRate));

        SetTempo(128.0); // sane default so a delay exists even before the first enable
    }

    /// <summary>Feed the delay line (ON) or let it ring out untouched (OFF).
    /// Safe to call from any thread.</summary>
    public void SetEnabled(bool on) => Volatile.Write(ref _enabled, on ? 1 : 0);

    /// <summary>Recompute the delay time from a BPM: <see cref="Beats"/> ×
    /// (60/bpm) × sample rate, clamped to the ring buffer's capacity. Call
    /// whenever the echo is (re)enabled (see Deck.SetEcho) — this does NOT
    /// chase live tempo changes on its own. Safe to call from any thread.</summary>
    public void SetTempo(double bpm)
    {
        if (bpm <= 0) bpm = 128.0;
        int samples = (int)Math.Round(Beats * (60.0 / bpm) * _sampleRate);
        samples = Math.Clamp(samples, 1, _capacityFrames - 1);
        Volatile.Write(ref _delaySamples, samples);
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
