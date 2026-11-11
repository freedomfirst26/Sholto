using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls;

internal sealed class WaveformPointerHandler : IWaveformPointerHandler
{
    public bool TryAnchorSeconds(double x, double width, double playPosition, double playbackSpeed,
        WaveformPeaks? gridPeaks, WaveformPeaks? peaks, out double seconds)
    {
        seconds = 0;
        // Map click X → track seconds using the SAME source→screen mapping the
        // overlays use (see WaveformControl.Render): screen_x = (beatCol − refCenterPeak)/speed
        // + width/2, so beatCol = (x − width/2)·speed + refCenterPeak, and
        // seconds = beatCol · secondsPerPeak. Reference peaks = GridPeaks
        // (falls back to Peaks) so this matches where the grid lines render.
        var refPeaks = (gridPeaks is { Min.Length: > 0 }) ? gridPeaks
                     : (peaks is { Min.Length: > 0 })     ? peaks
                     : null;
        if (refPeaks is null) return false;

        double secondsPerPeak = refPeaks.SecondsPerPeak;
        int total = refPeaks.Min.Length;
        double centerPeak = playPosition * total;
        double speed = playbackSpeed > 0.01 ? playbackSpeed : 1.0;

        double beatCol = (x - width / 2.0) * speed + centerPeak;
        seconds = beatCol * secondsPerPeak;
        if (seconds < 0) seconds = 0;
        return true;
    }
}
