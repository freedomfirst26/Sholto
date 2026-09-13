using Sholto.Dsp;

namespace Sholto.Analysis.Analyzers.Waveform;

/// <summary>Default <see cref="IWaveformPeakAnalyzer"/>. Pure computation — no
/// environment/filesystem/clock dependency — split out from <see cref="WaveformPeaks"/>
/// purely so callers (Deck, a Bench harness) can inject a fake instead of paying the
/// real ~100-200 ms/track cost; it holds no state of its own.</summary>
public sealed class WaveformPeakAnalyzer : IWaveformPeakAnalyzer
{
    /// <summary>
    /// Computes min/max + per-band peak amplitudes from interleaved float samples.
    /// <paramref name="normalizeBands"/> scales Low/Mid/High to [0,1] within this
    /// signal; turn it off when the result will be merged with peers (one per stem)
    /// since independent normalization compresses cross-stem energy differences.
    /// </summary>
    public WaveformPeaks Compute(
        float[] samples, int channels, int sampleRate = WaveformDefaults.SampleRate,
        int samplesPerPeak = WaveformDefaults.SamplesPerPeak, bool normalizeBands = true)
    {
        if (samples.Length == 0) return WaveformPeaks.Empty;

        int frameCount = samples.Length / channels;
        int peakCount = (frameCount + samplesPerPeak - 1) / samplesPerPeak;

        var min = new float[peakCount];
        var max = new float[peakCount];
        var lowOut = new float[peakCount];
        var midOut = new float[peakCount];
        var highOut = new float[peakCount];

        // Three-band split for the visualiser: a narrow BPF in the middle leaves a
        // hole around 2–4 kHz (snare crack, vocal presence, hi-hat shimmer drop
        // out of every band). Instead, run LP + HP and define mid as the
        // complement — so the three bands sum to the original signal with no gap.
        var lpf = Biquad.LowPass(sampleRate, freq: WaveformBandFrequencies.LowMidHz, q: 0.707f);
        var hpf = Biquad.HighPass(sampleRate, freq: WaveformBandFrequencies.MidHighHz, q: 0.707f);

        for (int p = 0; p < peakCount; p++)
        {
            float lo = 0f, hi = 0f;
            float bandLow = 0f, bandMid = 0f, bandHigh = 0f;

            int frameStart = p * samplesPerPeak;
            int frameEnd = Math.Min(frameStart + samplesPerPeak, frameCount);

            for (int f = frameStart; f < frameEnd; f++)
            {
                float mono = 0f;
                int baseIdx = f * channels;
                for (int c = 0; c < channels; c++)
                {
                    float s = samples[baseIdx + c];
                    mono += s;
                    if (s < lo) lo = s;
                    if (s > hi) hi = s;
                }
                mono /= channels;

                float lRaw = lpf.Process(mono);
                float hRaw = hpf.Process(mono);
                // Complementary mid: everything the LP and HP didn't take. Phase
                // offsets from the filters smear this slightly, but abs+peak-track
                // washes that out — visually the three bands now cover 0..Nyquist
                // with no dead zone.
                float l = MathF.Abs(lRaw);
                float h = MathF.Abs(hRaw);
                float m = MathF.Abs(mono - lRaw - hRaw);

                if (l > bandLow) bandLow = l;
                if (m > bandMid) bandMid = m;
                if (h > bandHigh) bandHigh = h;
            }

            min[p] = lo;
            max[p] = hi;
            lowOut[p] = bandLow;
            midOut[p] = bandMid;
            highOut[p] = bandHigh;
        }

        Smooth(lowOut, radius: 2);
        Smooth(midOut, radius: 2);
        Smooth(highOut, radius: 2);
        Smooth(min, radius: 2);
        Smooth(max, radius: 2);

        if (normalizeBands)
        {
            Normalize(lowOut);
            Normalize(midOut);
            Normalize(highOut);
        }

        return new WaveformPeaks(min, max, lowOut, midOut, highOut, samplesPerPeak);
    }

    /// <summary>(2*radius + 1)-tap box smoothing in place.</summary>
    private static void Smooth(float[] arr, int radius)
    {
        int n = arr.Length;
        if (n == 0 || radius <= 0) return;
        var tmp = new float[n];
        int window = 2 * radius + 1;
        for (int i = 0; i < n; i++)
        {
            float sum = 0;
            int count = 0;
            int from = Math.Max(0, i - radius);
            int to = Math.Min(n - 1, i + radius);
            for (int j = from; j <= to; j++) { sum += arr[j]; count++; }
            tmp[i] = sum / count;
        }
        Array.Copy(tmp, arr, n);
    }

    private static void Normalize(float[] arr)
    {
        float max = 0f;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] > max) max = arr[i];
        if (max <= 0f) return;
        float inv = 1f / max;
        for (int i = 0; i < arr.Length; i++) arr[i] *= inv;
    }
}
