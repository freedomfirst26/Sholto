namespace Sholto.Analysis.Analyzers.Waveform;

/// <summary>Default <see cref="IWaveformBandScaler"/>. Pure computation — no
/// environment/filesystem/clock dependency — split out of
/// <see cref="WaveformBandScaling"/> purely so the render-time callers
/// (WaveformControl, MinimapControl) can be handed an instance instead of a
/// static type; it holds no state of its own.</summary>
public sealed class WaveformBandScaler : IWaveformBandScaler
{
    /// <summary>Calibrate against a whole track's band envelopes.
    /// <paramref name="percentile"/> (0..1) picks the reference level per band.
    /// <see cref="WaveformBandScaling.MaxTotal"/> is the largest summed-normalized
    /// column — divide the deck's half-height by it so the track's most intense
    /// moment fills the deck and everything else scales down from there.</summary>
    public WaveformBandScaling Calibrate(
        ReadOnlySpan<float> low, ReadOnlySpan<float> mid, ReadOnlySpan<float> high,
        float percentile = WaveformDefaults.Percentile)
    {
        float rL = MathF.Max(Percentile(low, percentile), 1e-4f);
        float rM = MathF.Max(Percentile(mid, percentile), 1e-4f);
        float rH = MathF.Max(Percentile(high, percentile), 1e-4f);

        float maxTotal = 1e-4f;
        for (int i = 0; i < low.Length; i++)
        {
            float t = Clamp01(low[i] / rL) + Clamp01(mid[i] / rM) + Clamp01(high[i] / rH);
            if (t > maxTotal) maxTotal = t;
        }
        return new WaveformBandScaling(rL, rM, rH, maxTotal);
    }

    /// <summary>Shared-reference calibration for a dynamics-preserving waveform:
    /// every band divides by the SAME broadband reference (a high percentile of
    /// low+mid+high across the track), so the silhouette height tracks absolute
    /// loudness — quiet sections stay short, drops stay tall — while the band split
    /// still colours the shape. Physically the bass dominates the height and the
    /// highs sit as a thin crest, which is how Rekordbox/Serato read. Pair with
    /// <see cref="WaveformBandScaling.NormalizeAbsolute"/>.</summary>
    public WaveformBandScaling CalibrateShared(
        ReadOnlySpan<float> low, ReadOnlySpan<float> mid, ReadOnlySpan<float> high,
        float percentile = WaveformDefaults.Percentile)
    {
        int n = low.Length;
        var sum = new float[n];
        for (int i = 0; i < n; i++) sum[i] = low[i] + mid[i] + high[i];
        float refT = MathF.Max(Percentile(sum, percentile), 1e-4f);

        float maxTotal = 1e-4f;
        for (int i = 0; i < n; i++)
        {
            float t = sum[i] / refT;
            if (t > maxTotal) maxTotal = t;
        }
        return new WaveformBandScaling(refT, refT, refT, maxTotal);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    private float Percentile(ReadOnlySpan<float> data, float p)
    {
        if (data.Length == 0) return 0f;
        var copy = data.ToArray();
        Array.Sort(copy);
        int idx = (int)MathF.Round(p * (copy.Length - 1));
        return copy[Math.Clamp(idx, 0, copy.Length - 1)];
    }
}
