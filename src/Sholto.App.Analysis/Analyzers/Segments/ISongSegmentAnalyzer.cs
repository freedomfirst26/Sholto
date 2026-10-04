using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>
/// Port for <see cref="SongSegmentAnalyzer"/>. Constructor-injected into
/// <c>DeckSession</c> (fires on <c>BasicReady</c>) so a test/Bench harness
/// can substitute a fake instead of running the real energy-envelope
/// segmentation.
/// </summary>
public interface ISongSegmentAnalyzer : IAnalyzer
{
    IReadOnlyList<SongSegment> Analyze(WaveformPeaks peaks, double[] downbeats, int sampleRate);
}
