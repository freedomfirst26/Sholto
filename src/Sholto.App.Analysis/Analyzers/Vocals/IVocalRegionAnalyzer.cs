using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;

namespace Sholto.App.Analysis.Analyzers.Vocals;

/// <summary>
/// Port for <see cref="VocalRegionAnalyzer"/>. Constructor-injected into
/// <c>StemAnalysisStage</c> (runs against the vocal stem's peaks once stems are decoded) so a
/// test/Bench harness can substitute a fake instead of running the real
/// thresholding pass.
/// </summary>
public interface IVocalRegionAnalyzer : IAnalyzer
{
    IReadOnlyList<VocalRegion> Analyze(WaveformPeaks vocal, int sampleRate);
}
