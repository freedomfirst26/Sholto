namespace Sholto.App.Analysis.Analyzers;

/// <summary>
/// A basic analysis whose path-only work (cache lookups, then on a miss the beat tracker)
/// has already started. Finish it once the decoded samples exist.
/// </summary>
public interface IBasicAnalysisRequest
{
    /// <summary>Finish the analysis with the decoded <paramref name="track"/>.</summary>
    Task<BasicAnalysis> CompleteAsync(DecodedTrack track);
}
