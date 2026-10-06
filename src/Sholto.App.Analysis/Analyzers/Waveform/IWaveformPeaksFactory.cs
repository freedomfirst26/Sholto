using Sholto.Data;

namespace Sholto.App.Analysis.Analyzers.Waveform;

public interface IWaveformPeaksFactory
{
    /// <summary>The peaks value that describes nothing renderable.</summary>
    WaveformPeaks None();
}
