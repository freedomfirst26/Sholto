namespace Sholto.Analysis.Analyzers.Waveform;

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

    // Calibrate / CalibrateShared moved to WaveformBandScaler (IWaveformBandScaler)
    // — see that port's doc comment for why this is a separate port rather than
    // folded into IWaveformPeakAnalyzer.
}
