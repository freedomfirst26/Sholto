namespace Sholto.Analysis;

/// <summary>
/// Absolute, track-calibrated scaling for the frequency-band waveform. Each band
/// is drawn at its true energy relative to the WHOLE track's range for that band,
/// so blue (low) is tall only when the bass genuinely is, and the silhouette
/// height tracks real intensity — quiet sections read short, drops read tall.
///
/// This replaces per-column proportional stacking (band ÷ the column's own
/// low+mid+high total), which paints a bass-looking blue stripe on any section
/// that is low-heavy-but-quiet — the "looks bassy, sounds like mids" problem.
///
/// The reference per band is a high percentile of that band's energy across the
/// track (not the raw max), so a single transient spike doesn't compress the
/// whole track's range.
/// </summary>
public readonly record struct WaveformBandScaling(float RefLow, float RefMid, float RefHigh, float MaxTotal)
{
    /// <summary>One column's raw band peaks → [0,1] against the per-band track
    /// reference. Each output is "how much of this track's loudest [band]".</summary>
    public (float Low, float Mid, float High) Normalize(float low, float mid, float high) =>
        (Clamp01(low / RefLow), Clamp01(mid / RefMid), Clamp01(high / RefHigh));

    /// <summary>Absolute band heights against the shared broadband reference, with
    /// NO per-band clamp — so a quiet section stays short and a loud transient can
    /// exceed unity (the caller clamps the final silhouette to the deck). This is
    /// what preserves dynamics: unlike <see cref="Normalize"/> it never lifts a band
    /// to its own ceiling, so intros read short and drops read tall.</summary>
    public (float Low, float Mid, float High) NormalizeAbsolute(float low, float mid, float high) =>
        (low / RefLow, mid / RefMid, high / RefHigh);

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>Calibrate against a whole track's band envelopes.
    /// <paramref name="percentile"/> (0..1) picks the reference level per band.
    /// <see cref="MaxTotal"/> is the largest summed-normalized column — divide the
    /// deck's half-height by it so the track's most intense moment fills the deck
    /// and everything else scales down from there.</summary>
    public static WaveformBandScaling Calibrate(
        ReadOnlySpan<float> low, ReadOnlySpan<float> mid, ReadOnlySpan<float> high,
        float percentile = 0.97f)
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
    /// <see cref="NormalizeAbsolute"/>.</summary>
    public static WaveformBandScaling CalibrateShared(
        ReadOnlySpan<float> low, ReadOnlySpan<float> mid, ReadOnlySpan<float> high,
        float percentile = 0.97f)
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

    private static float Percentile(ReadOnlySpan<float> data, float p)
    {
        if (data.Length == 0) return 0f;
        var copy = data.ToArray();
        Array.Sort(copy);
        int idx = (int)MathF.Round(p * (copy.Length - 1));
        return copy[Math.Clamp(idx, 0, copy.Length - 1)];
    }
}
