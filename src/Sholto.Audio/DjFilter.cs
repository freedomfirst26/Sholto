using System.Threading;
using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Pioneer-style COLOR / FILTER knob. One control: center detent = dry
/// bypass; twist left → resonant low-pass swept log-scaled 18 kHz → 80 Hz;
/// twist right → resonant high-pass 20 Hz → 10 kHz.
///
/// Implementation is Vadim Zavalishin's TPT (topology-preserving transform)
/// state-variable filter with <c>tanh</c> saturation injected INSIDE the
/// resonance feedback. The SVF stays stable under coefficient modulation
/// (no zipper noise as the cutoff sweeps), and the in-loop saturation makes
/// the resonance self-limiting rather than ringing forever and then
/// clipping at the output stage — which is what gives Pioneer's COLOR FX
/// its characteristic "crush" when the filter sweeps across a loud kick.
/// Modeled on Mixxx's QuickEffect filter and the broader VA-filter
/// literature (Zavalishin "The Art of VA Filter Design", Stilson-Smith).
///
/// Lock-free: UI / MIDI threads call <see cref="SetPosition"/>; the audio
/// thread reads once per buffer and recomputes coefficients.
/// </summary>
public sealed class DjFilter : DeckEffect
{
    /// <inheritdoc/>
    public override string EffectId => "filter";

    private static readonly EffectParam[] ParamTable =
    [
        new(0, "Position", 0.0, 1.0, 0.5, EffectParamCurve.Linear),
    ];

    /// <inheritdoc/>
    public override ReadOnlySpan<EffectParam> Params => ParamTable;

    /// <summary>Forwards to <see cref="SetPosition"/> — MUST go through it
    /// rather than writing <see cref="_coeffs"/> directly. That method owns
    /// <see cref="_designedPosition"/> as control-thread-only state and
    /// dedupes on a 0.001 threshold before publishing new coefficients; a
    /// second entry point would break the dedupe and put the transcendental
    /// design maths back on the audio thread.</summary>
    public override void SetParam(int paramId, double value) => SetPosition((float)value);

    // Sweep range — picked so the "bypass-edge" cutoffs (just past the
    // dead-zone) are far outside the audible band, making the LP/HP
    // crossover at center inaudible.
    private const float LpMinHz = 80f;      // position = 0 (full LP cut)
    private const float LpMaxHz = 18000f;   // position = 0.5 − DeadZone (effectively bypass)
    private const float HpMinHz = 20f;      // position = 0.5 + DeadZone (effectively bypass)
    private const float HpMaxHz = 10000f;   // position = 1 (full HP cut)
    private const float DeadZone = 0.02f;   // ±0.02 around 0.5 = dry bypass

    // Resonance. Q = 2 gives a ~6 dB peak at cutoff — perceptible
    // "vocal" character as the knob sweeps, well clear of self-oscillation
    // (which kicks in as k = 1/Q → 0). Pioneer's filter is roughly here.
    private const float Q = 2.0f;

    // Saturation drive. tanh is mostly linear below ±0.5 input, so we
    // pre-scale the resonance signal by Drive to push it onto the
    // saturating part of the curve at normal track levels, then
    // un-scale on the way out so the linear-region gain is unity.
    // Drive=2 puts the knee around −6 dBFS in the resonance path —
    // audible crunch on hot signal, transparent on quiet sections.
    private const float Drive = 2.0f;

    private readonly int _channels;
    private readonly int _sampleRate;

    private float _designedPosition = -1f;   // sentinel outside 0..1 → first SetPosition forces design

    private enum FilterMode { Bypass, LowPass, HighPass }

    /// <summary>Immutable coefficient snapshot designed on the control thread
    /// (<see cref="SetPosition"/>) and published to the audio thread with a
    /// single reference assignment. <see cref="Process"/> and
    /// <see cref="ProcessSample"/> each take one <see cref="Volatile.Read"/>
    /// of <see cref="_coeffs"/> and hoist the fields into locals — no
    /// transcendental maths ever runs on the audio thread.</summary>
    private sealed class FilterCoeffs
    {
        public readonly float G, K, A1, A2, A3;
        public readonly FilterMode Mode;

        public FilterCoeffs(float g, float k, float a1, float a2, float a3, FilterMode mode)
        {
            G = g; K = k; A1 = a1; A2 = a2; A3 = a3; Mode = mode;
        }

        public static readonly FilterCoeffs Bypass = new(0f, 0f, 0f, 0f, 0f, FilterMode.Bypass);
    }

    private FilterCoeffs _coeffs = FilterCoeffs.Bypass;

    // Audio-thread-only: the mode last observed by Process, used to detect a
    // transition into bypass so the integrator clear can stay on the audio
    // thread (clearing it from SetPosition would race Process).
    private FilterMode _lastMode = FilterMode.Bypass;

    // SVF integrator state (per channel — stereo state can't share).
    private readonly float[] _ic1eq;
    private readonly float[] _ic2eq;

    public DjFilter(SfEngine engine, AudioFormat format)
    {
        _ = engine;
        _sampleRate = format.SampleRate;
        _channels = Math.Max(1, format.Channels);
        _ic1eq = new float[_channels];
        _ic2eq = new float[_channels];
    }

    /// <summary>Set knob position. 0 = full LP, 0.5 = bypass, 1 = full HP.
    /// Clamped. Safe to call from any thread. Designs the new coefficients
    /// here — on the control thread — and publishes them with a single
    /// reference assignment; the audio thread never runs the transcendental
    /// design maths.</summary>
    public void SetPosition(float position)
    {
        if (position < 0f) position = 0f;
        if (position > 1f) position = 1f;

        if (MathF.Abs(position - _designedPosition) > 0.001f)
        {
            Volatile.Write(ref _coeffs, DesignForPosition(position));
            _designedPosition = position;
        }
    }

    public override void Process(Span<float> buffer, int channels)
    {
        FilterCoeffs c = Volatile.Read(ref _coeffs);

        // Entering bypass (dead-zone): clear integrators so a subsequent
        // bypass→active transition doesn't blat out a stale tail. This MUST
        // stay on the audio thread — clearing from SetPosition would race
        // the buffer loop below.
        if (c.Mode == FilterMode.Bypass && _lastMode != FilterMode.Bypass)
        {
            Array.Clear(_ic1eq);
            Array.Clear(_ic2eq);
        }
        _lastMode = c.Mode;

        if (c.Mode == FilterMode.Bypass) return;

        bool isLp = c.Mode == FilterMode.LowPass;
        int frames = buffer.Length / channels;

        // Hoist coefficients into locals so the JIT keeps them in registers
        // across the inner loop.
        float k = c.K, a1 = c.A1, a2 = c.A2, a3 = c.A3;
        float invDrive = 1f / Drive;

        for (int i = 0; i < frames; i++)
        {
            int baseIdx = i * channels;
            for (int ch = 0; ch < channels; ch++)
            {
                float input = buffer[baseIdx + ch];
                float ic1 = _ic1eq[ch];
                float ic2 = _ic2eq[ch];

                // TPT SVF prediction.
                float v3 = input - ic2;
                float v1 = a1 * ic1 + a2 * v3;
                float v2 = ic2 + a2 * ic1 + a3 * v3;

                // Soft-saturate the band-pass / resonance signal. This is
                // the "crush" — tanh on v1 (which gets fed back into the
                // integrators on the next step) compresses the resonant
                // peak softly and adds odd-order harmonics. Drive scaling
                // puts the knee at audible signal levels; the /Drive
                // restores unity gain in the small-signal region.
                v1 = MathF.Tanh(v1 * Drive) * invDrive;

                _ic1eq[ch] = 2f * v1 - ic1;
                _ic2eq[ch] = 2f * v2 - ic2;

                buffer[baseIdx + ch] = isLp ? v2 : (input - k * v1 - v2);
            }
        }
    }

    public override float ProcessSample(float sample, int channel)
    {
        FilterCoeffs c = Volatile.Read(ref _coeffs);
        if (c.Mode == FilterMode.Bypass) return sample;
        float ic1 = _ic1eq[channel], ic2 = _ic2eq[channel];
        float v3 = sample - ic2;
        float v1 = c.A1 * ic1 + c.A2 * v3;
        float v2 = ic2 + c.A2 * ic1 + c.A3 * v3;
        v1 = MathF.Tanh(v1 * Drive) / Drive;
        _ic1eq[channel] = 2f * v1 - ic1;
        _ic2eq[channel] = 2f * v2 - ic2;
        return c.Mode == FilterMode.LowPass ? v2 : (sample - c.K * v1 - v2);
    }

    private FilterCoeffs DesignForPosition(float pos)
    {
        if (pos > 0.5f - DeadZone && pos < 0.5f + DeadZone)
        {
            // Dead-zone → dry bypass. The integrator clear for a
            // bypass→active transition happens on the audio thread, in
            // Process, so it can't race the buffer loop.
            return FilterCoeffs.Bypass;
        }

        float fc;
        FilterMode mode;
        if (pos < 0.5f)
        {
            float t = pos / (0.5f - DeadZone);
            fc = LogLerp(LpMinHz, LpMaxHz, t);
            mode = FilterMode.LowPass;
        }
        else
        {
            float t = (pos - (0.5f + DeadZone)) / (0.5f - DeadZone);
            fc = LogLerp(HpMinHz, HpMaxHz, t);
            mode = FilterMode.HighPass;
        }

        // Clamp below nyquist so tan() stays finite. 0.45·sr is well
        // inside the safe range and far above the audible upper bound
        // even for the HP extreme.
        if (fc > _sampleRate * 0.45f) fc = _sampleRate * 0.45f;

        float g = MathF.Tan(MathF.PI * fc / _sampleRate);
        float k = 1f / Q;
        float a1 = 1f / (1f + g * (g + k));
        float a2 = g * a1;
        float a3 = g * a2;
        return new FilterCoeffs(g, k, a1, a2, a3, mode);
    }

    private static float LogLerp(float lo, float hi, float t)
        => MathF.Exp(MathF.Log(lo) + (MathF.Log(hi) - MathF.Log(lo)) * t);
}
