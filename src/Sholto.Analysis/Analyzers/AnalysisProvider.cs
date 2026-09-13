using Sholto.Analysis;

namespace Sholto.Analysis.Analyzers;

/// <summary>
/// Compute-only <see cref="IAnalysisProvider"/>: runs <c>compute</c> and nothing
/// else. Every cache tier is a decorator wrapped around this — the in-process tier
/// is <see cref="CachingAnalysisProvider"/>, the DB tier is
/// <see cref="DbAnalysisProvider"/>. This class no longer knows caches exist.
/// </summary>
public sealed class AnalysisProvider : IAnalysisProvider
{
    private readonly Func<DecodedTrack, CancellationToken, Task<BasicAnalysis>> _compute;

    public AnalysisProvider(Func<DecodedTrack, CancellationToken, Task<BasicAnalysis>> compute)
    {
        _compute = compute;
    }

    /// <summary>Resolve the basic analysis for a track by computing it.</summary>
    public Task<BasicAnalysis> GetAsync(DecodedTrack track, CancellationToken ct = default) =>
        _compute(track, ct);

    /// <summary>
    /// Force a fresh compute. There is nothing to bypass at this layer — every
    /// cache tier lives above this class now — so this is identical to
    /// <see cref="GetAsync"/>. Kept as a separate method to satisfy
    /// <see cref="IAnalysisProvider"/> and to mirror the "recompute" verb the
    /// decorators above it expose.
    /// </summary>
    public Task<BasicAnalysis> RecomputeAsync(DecodedTrack track, CancellationToken ct = default) =>
        _compute(track, ct);
}
