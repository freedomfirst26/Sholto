using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Reduces waveform peaks to one <see cref="BarFeatures"/> entry per bar of a beatgrid.</summary>
public interface IBarFeatureExtractor
{
    BarFeatures Extract(WaveformPeaks peaks, Beatgrid grid, int sampleRate);
}
