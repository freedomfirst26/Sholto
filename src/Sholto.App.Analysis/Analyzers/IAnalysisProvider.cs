using Sholto.App.Analysis;

namespace Sholto.App.Analysis.Analyzers;

/// <summary>
/// Port for <see cref="AnalysisProvider"/>. Lets a test/Bench harness supply cache
/// behaviour (always-hit, always-miss, slow compute) without wiring the real cache
/// stack, decoder and analyser chain just to construct a <c>Deck</c>.
/// </summary>
public interface IAnalysisProvider
{
    /// <summary>Resolve the basic analysis for a track. Equivalent to
    /// <c>Begin(track.FilePath, ct).CompleteAsync(track)</c>.</summary>
    Task<BasicAnalysis> GetAsync(
        DecodedTrack track, CancellationToken ct = default);

    /// <summary>
    /// Start the path-only work (cache lookups, then on a miss the beat tracker) and return at
    /// once; finish with <see cref="IBasicAnalysisRequest.CompleteAsync"/> when samples exist.
    /// <paramref name="ct"/> cancels the started work.
    /// </summary>
    IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Force a fresh compute, bypassing every cache, and write the result through
    /// to every tier (overwriting any stale entry). Used by the "hold song-select
    /// to re-analyze" gesture when the cached BPM/beats are wrong.
    /// </summary>
    Task<BasicAnalysis> RecomputeAsync(
        DecodedTrack track, CancellationToken ct = default);
}
