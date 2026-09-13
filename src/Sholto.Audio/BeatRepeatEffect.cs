using System.Threading;
using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Per-deck beat-repeat / roll: while engaged, captures a beat-synced slice of
/// the track on the first pass through it and then loops that slice until
/// released, at which point the live signal (which has kept advancing the
/// whole time — this effect never touches the playhead) is faded back in.
/// That fade-in IS the "catch-up": nothing needs to seek anywhere, because the
/// underlying player never stopped, so the dry signal already reflects
/// "where the playhead would have been".
///
/// Modeled directly on <see cref="EchoEffect"/>'s lock-free pattern: UI/MIDI
/// threads write target state via <see cref="SetEnabled"/> / <see cref="Beats"/>
/// / <see cref="SetTempo"/>; the audio thread reads it once per buffer via
/// <c>Volatile.Read</c>. The capture buffer is a fixed-capacity array per
/// channel, allocated once at construction, sized for the slowest tempo /
/// longest roll this effect supports — no allocation in <see cref="Process"/>
/// or <see cref="ProcessSample"/>.
/// </summary>
public sealed class BeatRepeatEffect : DeckEffect
{
    /// <inheritdoc/>
    public override string EffectId => "roll";

    private static readonly EffectParam[] ParamTable =
    [
        new(0, "Enabled", 0.0, 1.0, 0.0, EffectParamCurve.Boolean),
        new(1, "Beats",   0.0625, 2.0, 0.25, EffectParamCurve.Linear),
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

    // Ring/capture buffer sized generously (4 s per channel) so even the
    // longest roll (2 beats) at a slow track (30 BPM: 2 beats = 4 s) fits —
    // same worst-case reasoning as EchoEffect's CapacitySeconds.
    private const double CapacitySeconds = 4.0;

    private readonly int _sampleRate;
    private readonly int _capacityFrames;
    // One capture buffer per channel — keeps left/right fully independent, so
    // ProcessSample (called per-channel, no frame grouping) stays correct
    // without needing to know about the other channel.
    private readonly float[][] _captureLine;

    // Target on/off — read lock-free; ramped into _repeatGain over ~5ms so a
    // press/release mid-buffer doesn't click. Also doubles as the dry/wet
    // crossfade that makes release sound like a catch-up rather than a jump.
    private int _enabled; // 0/1, Volatile-accessed (no bool Volatile overload)
    private float _repeatGain;
    private readonly float _gainAlpha;

    // Target roll length in samples — recomputed by SetTempo/Beats, read
    // lock-free. Frozen into _activePeriod on each press so a mid-hold Beats
    // change doesn't resize a loop already in progress.
    private int _periodSamples;

    // Audio-thread-owned state (Process/ProcessSample only).
    private bool _wasEnabled;
    private int _activePeriod = 1;
    private int _phase;
    private bool _capturing;

    private double _lastBpm = DeckMixer.DefaultBpm;
    private double _beats = 0.25;

    /// <summary>Roll length, in fractions of a beat. 0.25 (the default) is a
    /// 16th-note style stutter. Setting this recomputes the target period
    /// immediately from the last tempo passed to <see cref="SetTempo"/>, but
    /// only affects the NEXT press — see <see cref="_activePeriod"/>.</summary>
    public double Beats
    {
        get => _beats;
        set { _beats = value; RecomputeDelay(); }
    }

    public BeatRepeatEffect(SfEngine engine, AudioFormat format)
    {
        _ = engine; // base is parameterless; param kept for API parity with the other modifiers
        _sampleRate = format.SampleRate;
        int channels = Math.Max(1, format.Channels);

        _capacityFrames = (int)(_sampleRate * CapacitySeconds);
        _captureLine = new float[channels][];
        for (int c = 0; c < channels; c++) _captureLine[c] = new float[_capacityFrames];

        // ~5 ms one-pole ramp — same shape/tau as EchoEffect's input-gain ramp.
        _gainAlpha = TempoMath.FiveMsGainAlpha(_sampleRate);

        SetTempo(DeckMixer.DefaultBpm); // sane default so a period exists even before the first press
    }

    private void RecomputeDelay()
    {
        int samples = TempoMath.BeatsToSamples(_beats, _lastBpm, _sampleRate, _capacityFrames);
        Volatile.Write(ref _periodSamples, samples);
    }

    /// <summary>Press (true) engages the roll — a fresh slice is captured
    /// starting from this instant. Release (false) fades the live signal back
    /// in; see the class doc for why that alone is the catch-up. Safe to call
    /// from any thread.</summary>
    public void SetEnabled(bool on) => Volatile.Write(ref _enabled, on ? 1 : 0);

    /// <summary>Recompute the roll length from a BPM, the same way
    /// <see cref="EchoEffect.SetTempo"/> does. Safe to call from any
    /// thread.</summary>
    public void SetTempo(double bpm)
    {
        if (bpm <= 0) bpm = DeckMixer.DefaultBpm;
        _lastBpm = bpm;
        RecomputeDelay();
    }

    /// <summary>Disable the roll and clear its capture state — called by
    /// <see cref="Deck.ResetControls"/> on track load, mirroring
    /// <see cref="EchoEffect.Reset"/>. Does not reallocate.</summary>
    public void Reset()
    {
        SetEnabled(false);
        _repeatGain = 0f;
        _wasEnabled = false;
        _phase = 0;
        _capturing = false;
        foreach (var line in _captureLine) Array.Clear(line);
    }

    public override void Process(Span<float> buffer, int channels)
    {
        bool onTarget = Volatile.Read(ref _enabled) != 0;
        float targetGain = onTarget ? 1f : 0f;
        float gain = _repeatGain;
        int lineCount = _captureLine.Length;
        int activePeriod = _activePeriod;
        int phase = _phase;
        bool capturing = _capturing;
        bool wasEnabled = _wasEnabled;

        int frames = buffer.Length / channels;
        for (int i = 0; i < frames; i++)
        {
            if (onTarget && !wasEnabled)
            {
                // Rising edge: freeze this hold's period and start a fresh capture.
                activePeriod = Math.Clamp(Volatile.Read(ref _periodSamples), 1, _capacityFrames - 1);
                phase = 0;
                capturing = true;
            }
            wasEnabled = onTarget;

            gain += (targetGain - gain) * _gainAlpha;

            int baseIdx = i * channels;
            int chCount = Math.Min(channels, lineCount);
            for (int ch = 0; ch < chCount; ch++)
            {
                var line = _captureLine[ch];
                float input = buffer[baseIdx + ch];
                float repeated;
                if (capturing)
                {
                    line[phase] = input;
                    repeated = input;
                }
                else
                {
                    repeated = line[phase];
                }

                buffer[baseIdx + ch] = input * (1f - gain) + repeated * gain;
            }

            phase++;
            if (phase >= activePeriod) { phase = 0; capturing = false; }

            // Next frame's onTarget is re-read fresh below (buffer loop),
            // but since Process handles a whole buffer per Volatile.Read for
            // efficiency (matching EchoEffect), re-check per-frame only for
            // the rising-edge test — onTarget itself is fixed for this call.
        }

        _repeatGain = gain;
        _activePeriod = activePeriod;
        _phase = phase;
        _capturing = capturing;
        _wasEnabled = wasEnabled;
    }

    public override float ProcessSample(float sample, int channel)
    {
        // Single-sample path: not the hot path (Process handles real
        // playback) — same caveat as EchoEffect.ProcessSample about
        // multi-channel calls desyncing L/R phase; acceptable for the same
        // reason (nothing in this codebase drives modifiers through
        // ProcessSample today).
        if ((uint)channel >= (uint)_captureLine.Length) return sample;

        bool onTarget = Volatile.Read(ref _enabled) != 0;
        if (onTarget && !_wasEnabled)
        {
            _activePeriod = Math.Clamp(Volatile.Read(ref _periodSamples), 1, _capacityFrames - 1);
            _phase = 0;
            _capturing = true;
        }
        _wasEnabled = onTarget;

        float targetGain = onTarget ? 1f : 0f;
        _repeatGain += (targetGain - _repeatGain) * _gainAlpha;

        var line = _captureLine[channel];
        float repeated;
        if (_capturing)
        {
            line[_phase] = sample;
            repeated = sample;
        }
        else
        {
            repeated = line[_phase];
        }

        float output = sample * (1f - _repeatGain) + repeated * _repeatGain;

        _phase++;
        if (_phase >= _activePeriod) { _phase = 0; _capturing = false; }

        return output;
    }
}
