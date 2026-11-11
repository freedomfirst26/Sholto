using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls;

internal interface IWaveformPointerHandler
{
    /// <summary>The track time under screen x, using the transform the overlays draw with.
    /// False when neither peaks set has columns.</summary>
    bool TryAnchorSeconds(double x, double width, double playPosition, double playbackSpeed,
        WaveformPeaks? gridPeaks, WaveformPeaks? peaks, out double seconds);
}
