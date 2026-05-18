using System.Threading;
using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace CommunityDj.Audio;

/// <summary>
/// Rekordbox-style isolator EQ: splits the signal into three crossover bands
/// (low &lt; 250 Hz, mid 250–4000 Hz, high &gt; 4 kHz) and scales each band by a
/// linear gain knob. At gain = 0 a band is muted; at gain = 1 it passes through
/// flat; at gain = 2 it is boosted +6 dB. Turning all three to zero outputs
/// silence — same behaviour as a DJM/Xone hardware isolator.
///
/// Implemented with Linkwitz-Riley 4th-order crossovers (two cascaded Butterworth
/// biquads per stage) so the three bands sum back to a flat-magnitude response
/// when all gains are at 1.
///
/// Lock-free: UI threads write target gains via <see cref="SetBandGain"/>; the
/// audio thread reads them once per buffer and updates internal gains. Filters
/// are designed once at construction so the hot path is just arithmetic — no
/// allocations, no list mutation.
/// </summary>
public sealed class BiquadEq3Band : SoundModifier
{
    // Crossover points — standard DJ isolator territory.
    private const float LowMidHz  = 250f;
    private const float MidHighHz = 4000f;

    private readonly int _channels;

    // Atomic-write target gains (linear: 0 = mute, 1 = unity, 2 = +6 dB).
    private float _targetGain0 = 1f, _targetGain1 = 1f, _targetGain2 = 1f;

    // Audio-thread-only live gains.
    private float _gain0 = 1f, _gain1 = 1f, _gain2 = 1f;

    // LR4 = two cascaded Butterworth biquads per crossover stage.
    private readonly Biquad _lpA, _lpB;             // 4th-order LP @ 250 Hz  → Low band
    private readonly Biquad _midHpA, _midHpB;       // 4th-order HP @ 250 Hz  ↘ Mid band
    private readonly Biquad _midLpA, _midLpB;       // 4th-order LP @ 4000 Hz ↗
    private readonly Biquad _hpA, _hpB;             // 4th-order HP @ 4000 Hz → High band

    public BiquadEq3Band(SfEngine engine, AudioFormat format)
    {
        _ = engine;  // base is parameterless; param kept for API parity with built-in modifiers
        int sr = format.SampleRate;
        _channels = Math.Max(1, format.Channels);

        _lpA = new Biquad(_channels); _lpB = new Biquad(_channels);
        _midHpA = new Biquad(_channels); _midHpB = new Biquad(_channels);
        _midLpA = new Biquad(_channels); _midLpB = new Biquad(_channels);
        _hpA = new Biquad(_channels); _hpB = new Biquad(_channels);

        // Each Butterworth biquad uses Q = 1/√2 ≈ 0.7071. Cascading two of them
        // at the same fc produces a Linkwitz-Riley 4th-order response (Q ≈ 0.5).
        const float bwQ = 0.7071068f;
        _lpA.DesignLowPass(LowMidHz, bwQ, sr);
        _lpB.DesignLowPass(LowMidHz, bwQ, sr);
        _midHpA.DesignHighPass(LowMidHz, bwQ, sr);
        _midHpB.DesignHighPass(LowMidHz, bwQ, sr);
        _midLpA.DesignLowPass(MidHighHz, bwQ, sr);
        _midLpB.DesignLowPass(MidHighHz, bwQ, sr);
        _hpA.DesignHighPass(MidHighHz, bwQ, sr);
        _hpB.DesignHighPass(MidHighHz, bwQ, sr);
    }

    /// <summary>Set band gain (linear). 0 = mute, 1 = unity, 2 = +6 dB.
    /// <paramref name="band"/>: 0=Low, 1=Mid, 2=High.</summary>
    public void SetBandGain(int band, float gain)
    {
        switch (band)
        {
            case 0: Volatile.Write(ref _targetGain0, gain); break;
            case 1: Volatile.Write(ref _targetGain1, gain); break;
            default: Volatile.Write(ref _targetGain2, gain); break;
        }
    }

    public override void Process(Span<float> buffer, int channels)
    {
        _gain0 = Volatile.Read(ref _targetGain0);
        _gain1 = Volatile.Read(ref _targetGain1);
        _gain2 = Volatile.Read(ref _targetGain2);

        // All-unity fast path: bands sum to the input, so we can skip the filters.
        if (_gain0 == 1f && _gain1 == 1f && _gain2 == 1f) return;

        int frames = buffer.Length / channels;
        for (int i = 0; i < frames; i++)
        {
            int baseIdx = i * channels;
            for (int ch = 0; ch < channels; ch++)
            {
                float x = buffer[baseIdx + ch];

                // Low band: LP → LP
                float lo = _lpB.Step(_lpA.Step(x, ch), ch);
                // Mid band: HP @ low-mid → LP @ mid-high  (band-pass)
                float mid = _midLpB.Step(_midLpA.Step(_midHpB.Step(_midHpA.Step(x, ch), ch), ch), ch);
                // High band: HP → HP
                float hi = _hpB.Step(_hpA.Step(x, ch), ch);

                buffer[baseIdx + ch] = lo * _gain0 + mid * _gain1 + hi * _gain2;
            }
        }
    }

    public override float ProcessSample(float sample, int channel)
    {
        float lo  = _lpB.Step(_lpA.Step(sample, channel), channel);
        float mid = _midLpB.Step(_midLpA.Step(_midHpB.Step(_midHpA.Step(sample, channel), channel), channel), channel);
        float hi  = _hpB.Step(_hpA.Step(sample, channel), channel);
        return lo * _gain0 + mid * _gain1 + hi * _gain2;
    }

    /// <summary>One biquad section with per-channel state. RBJ Audio EQ Cookbook
    /// coefficients, Direct Form II Transposed.</summary>
    private sealed class Biquad
    {
        private readonly float[] _z1, _z2;
        private float _b0, _b1, _b2, _a1, _a2;

        public Biquad(int channels)
        {
            _z1 = new float[channels];
            _z2 = new float[channels];
            _b0 = 1f;
        }

        public float Step(float x, int channel)
        {
            float y  = _b0 * x + _z1[channel];
            _z1[channel] = _b1 * x - _a1 * y + _z2[channel];
            _z2[channel] = _b2 * x - _a2 * y;
            return y;
        }

        public void DesignLowPass(float fc, float q, int sr)
        {
            double w0 = 2.0 * Math.PI * fc / sr;
            double cosW = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * q);
            double b0 = (1 - cosW) / 2.0;
            double b1 = 1 - cosW;
            double b2 = (1 - cosW) / 2.0;
            double a0 = 1 + alpha;
            double a1 = -2 * cosW;
            double a2 = 1 - alpha;
            Normalise(b0, b1, b2, a0, a1, a2);
        }

        public void DesignHighPass(float fc, float q, int sr)
        {
            double w0 = 2.0 * Math.PI * fc / sr;
            double cosW = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * q);
            double b0 = (1 + cosW) / 2.0;
            double b1 = -(1 + cosW);
            double b2 = (1 + cosW) / 2.0;
            double a0 = 1 + alpha;
            double a1 = -2 * cosW;
            double a2 = 1 - alpha;
            Normalise(b0, b1, b2, a0, a1, a2);
        }

        private void Normalise(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            _b0 = (float)(b0 / a0);
            _b1 = (float)(b1 / a0);
            _b2 = (float)(b2 / a0);
            _a1 = (float)(a1 / a0);
            _a2 = (float)(a2 / a0);
        }
    }
}
