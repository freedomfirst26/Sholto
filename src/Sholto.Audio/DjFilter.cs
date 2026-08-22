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
public sealed class DjFilter : SoundModifier
{
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

    private float _targetPosition = 0.5f;
    private float _designedPosition = -1f;   // sentinel outside 0..1 → first buffer forces design
    private FilterMode _mode = FilterMode.Bypass;

    private enum FilterMode { Bypass, LowPass, HighPass }

    // SVF coefficients, shared across channels (recomputed in DesignForPosition).
    private float _g, _k, _a1, _a2, _a3;

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
    /// Clamped. Safe to call from any thread.</summary>
    public void SetPosition(float position)
    {
        if (position < 0f) position = 0f;
        if (position > 1f) position = 1f;
        Volatile.Write(ref _targetPosition, position);
    }

    public override void Process(Span<float> buffer, int channels)
    {
        float pos = Volatile.Read(ref _targetPosition);
        if (MathF.Abs(pos - _designedPosition) > 0.001f)
        {
            DesignForPosition(pos);
            _designedPosition = pos;
        }

        if (_mode == FilterMode.Bypass) return;

        bool isLp = _mode == FilterMode.LowPass;
        int frames = buffer.Length / channels;

        // Hoist coefficients into locals so the JIT keeps them in registers
        // across the inner loop.
        float k = _k, a1 = _a1, a2 = _a2, a3 = _a3;
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
        if (_mode == FilterMode.Bypass) return sample;
        float ic1 = _ic1eq[channel], ic2 = _ic2eq[channel];
        float v3 = sample - ic2;
        float v1 = _a1 * ic1 + _a2 * v3;
        float v2 = ic2 + _a2 * ic1 + _a3 * v3;
        v1 = MathF.Tanh(v1 * Drive) / Drive;
        _ic1eq[channel] = 2f * v1 - ic1;
        _ic2eq[channel] = 2f * v2 - ic2;
        return _mode == FilterMode.LowPass ? v2 : (sample - _k * v1 - v2);
    }

    private void DesignForPosition(float pos)
    {
        if (pos > 0.5f - DeadZone && pos < 0.5f + DeadZone)
        {
            // Entering dead-zone: clear integrators so a subsequent
            // bypass→active transition doesn't blat out a stale tail.
            _mode = FilterMode.Bypass;
            Array.Clear(_ic1eq);
            Array.Clear(_ic2eq);
            return;
        }

        float fc;
        if (pos < 0.5f)
        {
            float t = pos / (0.5f - DeadZone);
            fc = LogLerp(LpMinHz, LpMaxHz, t);
            _mode = FilterMode.LowPass;
        }
        else
        {
            float t = (pos - (0.5f + DeadZone)) / (0.5f - DeadZone);
            fc = LogLerp(HpMinHz, HpMaxHz, t);
            _mode = FilterMode.HighPass;
        }

        // Clamp below nyquist so tan() stays finite. 0.45·sr is well
        // inside the safe range and far above the audible upper bound
        // even for the HP extreme.
        if (fc > _sampleRate * 0.45f) fc = _sampleRate * 0.45f;

        _g = MathF.Tan(MathF.PI * fc / _sampleRate);
        _k = 1f / Q;
        _a1 = 1f / (1f + _g * (_g + _k));
        _a2 = _g * _a1;
        _a3 = _g * _a2;
    }

    private static float LogLerp(float lo, float hi, float t)
        => MathF.Exp(MathF.Log(lo) + (MathF.Log(hi) - MathF.Log(lo)) * t);
}
