namespace Sholto.Analysis;

/// <summary>
/// Port for <see cref="SongSegmentAnalyzer"/>. Constructor-injected into
/// <c>DeckViewModel</c> (fires on <c>BasicReady</c>) so a test/Bench harness
/// can substitute a fake instead of running the real energy-envelope
/// segmentation.
/// </summary>
public interface ISongSegmentAnalyzer
{
    SongSegments Analyze(WaveformPeaks peaks, double[] downbeats, int sampleRate);
}
