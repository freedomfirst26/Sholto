using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Computes per-bar features from waveform peaks and a beatgrid: reduces the peaks to one
/// <see cref="BarFeatures"/> entry per bar of the grid.</summary>
/// <remarks>Not a factory, so it is not named one: it computes analysis results rather than
/// building objects, like its sibling <see cref="PhraseSectionAnalyzer"/>.</remarks>
public interface IBarFeatureExtractor
{
    BarFeatures Extract(WaveformPeaks peaks, Beatgrid grid, int sampleRate);
}
