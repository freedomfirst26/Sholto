using Sholto.App.Analysis.Processing;

namespace Sholto.App.Analysis.Analyzers;

/// <summary>
/// Decorator: the in-process memory tier for <see cref="BasicAnalysis"/> lookups is
/// not <see cref="AnalysisProvider"/>'s concern, so it lives here instead — the same
/// shape as <see cref="CachingStemAnalysisStep"/> wrapping the demucs step. Hit →
/// return the cached analysis without ever touching the inner provider (no DB read,
/// no compute); miss → delegate to it, cache the result, and return it.
/// </summary>
public sealed class CachingAnalysisProvider(IAnalysisProvider inner) : IAnalysisProvider
{
    private readonly IAnalysisProvider _inner = inner;
    private readonly MemoryCache<string, BasicAnalysis> _cache = new();

    public Task<BasicAnalysis> GetAsync(DecodedTrack track, CancellationToken ct = default) =>
        Begin(track.FilePath, ct).CompleteAsync(track);

    public IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default)
    {
        if (_cache.TryGet(filePath) is { } cached)
            return new BasicAnalysisRequest(_ => Task.FromResult(cached));

        var inner = _inner.Begin(filePath, ct);
        return new BasicAnalysisRequest(async track =>
        {
            var computed = await inner.CompleteAsync(track);
            _cache.Set(filePath, computed);
            return computed;
        });
    }

    /// <summary>
    /// Force a fresh compute, bypassing every cache (this one included), and
    /// overwrite the memory entry with the result. This is the "bypass and write
    /// through" verb, and it belongs here explicitly — it's used by the "hold
    /// song-select to re-analyze" gesture when the cached BPM/beats are wrong.
    /// </summary>
    public async Task<BasicAnalysis> RecomputeAsync(DecodedTrack track, CancellationToken ct = default)
    {
        var computed = await _inner.RecomputeAsync(track, ct);
        _cache.Set(track.FilePath, computed);
        return computed;
    }
}
