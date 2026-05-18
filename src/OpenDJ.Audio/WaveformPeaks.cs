namespace OpenDJ.Audio;

public sealed record WaveformPeaks(float[] Min, float[] Max, int SamplesPerPeak)
{
    public static WaveformPeaks Empty { get; } = new([], [], 512);

    /// <summary>
    /// Computes min/max peaks from interleaved float samples using only the left channel.
    /// One peak per <paramref name="samplesPerPeak"/> frames.
    /// </summary>
    public static WaveformPeaks Compute(
        float[] samples,
        int channels,
        int samplesPerPeak = 512)
    {
        if (samples.Length == 0) return Empty;

        int frameCount = samples.Length / channels;
        int peakCount = (frameCount + samplesPerPeak - 1) / samplesPerPeak;

        var min = new float[peakCount];
        var max = new float[peakCount];

        for (int p = 0; p < peakCount; p++)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            int sampleStart = p * samplesPerPeak * channels;
            int sampleEnd = Math.Min(sampleStart + samplesPerPeak * channels, samples.Length);

            for (int i = sampleStart; i < sampleEnd; i++)
            {
                float s = samples[i];
                if (s < lo) lo = s;
                if (s > hi) hi = s;
            }

            min[p] = lo == float.MaxValue ? 0f : lo;
            max[p] = hi == float.MinValue ? 0f : hi;
        }

        return new WaveformPeaks(min, max, samplesPerPeak);
    }
}
