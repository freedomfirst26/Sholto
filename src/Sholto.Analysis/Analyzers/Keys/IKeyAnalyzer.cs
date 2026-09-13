using Sholto.Analysis.Reporting;

namespace Sholto.Analysis.Analyzers.Keys;

/// <summary>
/// Port for <see cref="KeyAnalyzer"/>. Constructor-injected into <c>Deck</c> and
/// <c>MainViewModel</c> (the "hold song-select to re-analyze" gesture) so a
/// test/Bench harness can substitute a fake instead of running the real
/// chroma + Krumhansl-Schmuckler estimate on every track load.
/// </summary>
public interface IKeyAnalyzer
{
    /// <summary>Fire-and-await wrapper: runs the heavy chroma + correlation on a
    /// background task and reports progress like the other analyzers.</summary>
    Task<KeyAnalysis> AnalyzeAsync(
        DecodedTrack track,
        IAnalysisReporter reporter, CancellationToken ct = default);

    /// <summary>
    /// Estimate the key of an interleaved float buffer. Returns a Result with both
    /// the musical key (e.g. "Am") and the Camelot code (e.g. "8A").
    /// </summary>
    KeyAnalyzer.Result Estimate(float[] stereoSamples, int channels, int sampleRate);
}
