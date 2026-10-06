namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>
/// Port for the <see cref="WaveformBandScaling"/> calibration step. A separate
/// port from <c>IWaveformPeakAnalyzer</c> rather than folded into it:
/// the peak analyzer runs once per track/stem LOAD (raw samples → peaks), this
/// runs on every RENDER (already-computed peaks → a scaling reference for the
/// waveform/minimap draw) — different callers (Sholto.Interface.MainUI.Controls, not
/// Deck/DeckFactory), different lifetime, and no shared state, so bolting it
/// onto the peak analyzer's contract would mix two unrelated responsibilities.
/// Its own narrow port keeps the "swap in a fake" reasoning (same as the peak
/// analyzer) without that coupling.
/// </summary>
public interface IWaveformBandScaler
{
    /// <summary>Default percentile (0..1) picking the reference level per band. Both the port and
    /// its implementation default to this, so the two cannot drift.</summary>
    const float DefaultPercentile = 0.97f;

    /// <summary>Calibrate against a whole track's band envelopes, one reference
    /// level per band. See <see cref="WaveformBandScaling.Calibrate"/>.</summary>
    WaveformBandScaling Calibrate(
        ReadOnlySpan<float> low, ReadOnlySpan<float> mid, ReadOnlySpan<float> high,
        float percentile = DefaultPercentile);

    /// <summary>Shared-reference calibration for a dynamics-preserving waveform.
    /// See <see cref="WaveformBandScaling.CalibrateShared"/>.</summary>
    WaveformBandScaling CalibrateShared(
        ReadOnlySpan<float> low, ReadOnlySpan<float> mid, ReadOnlySpan<float> high,
        float percentile = DefaultPercentile);
}
