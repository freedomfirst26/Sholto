using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Turns peaks into the binned, gated, attack/release band heights the 3-BAND style draws
/// (the "sideways bell" silhouette). RGB does not use it: that style draws raw per-column peaks.</summary>
public interface IWaveformEnvelopeBuilder
{
    /// <summary>Null when the peaks are empty or <paramref name="ct"/> was cancelled.</summary>
    BandEnvelopes? Build(WaveformPeaks peaks, CancellationToken ct);
}
