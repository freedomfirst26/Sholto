using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>Squeezes a track's peaks into one column per strip pixel so the existing waveform styles can
/// bake the whole track at the strip's width.</summary>
public interface IMinimapPeakDownsampler
{
    /// <summary>Output column x covers source columns [x·n/columns, (x+1)·n/columns), at least one. Min and
    /// Max keep the extremes, so a spike survives; the bands take the mean and the max of the range, half
    /// each, so a narrow transient still shows in the squeezed band. Band arrays stay empty when the source
    /// has none.</summary>
    WaveformPeaks Downsample(WaveformPeaks peaks, int columns);
}
