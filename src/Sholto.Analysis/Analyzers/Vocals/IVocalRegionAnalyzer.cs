using Sholto.Analysis.Analyzers.Waveform;

namespace Sholto.Analysis.Analyzers.Vocals;

/// <summary>
/// Port for <see cref="VocalRegionAnalyzer"/>. Constructor-injected into
/// <c>Deck</c> (runs against the vocal stem's peaks once stems land) so a
/// test/Bench harness can substitute a fake instead of running the real
/// thresholding pass.
/// </summary>
public interface IVocalRegionAnalyzer
{
    VocalRegions Analyze(WaveformPeaks vocal, int sampleRate);
}
