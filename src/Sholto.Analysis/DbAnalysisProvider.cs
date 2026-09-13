using Sholto.Analysis.Analyzers;

namespace Sholto.Analysis;

/// <summary>
/// Decorator: the DB-backed tier for <see cref="BasicAnalysis"/> lookups. Hit →
/// return the stored analysis without ever touching the inner provider (no
/// compute); miss → delegate to it, write the result back to the store, and
/// return it. Same shape as <see cref="CachingAnalysisProvider"/>, one layer in —
/// holds an <see cref="IBasicAnalysisStore"/> (normally the shared
/// <c>SwitchableBasicAnalysisStore</c>) instead of a <see cref="MemoryCache{TKey,TValue}"/>.
/// </summary>
public sealed class DbAnalysisProvider : IAnalysisProvider
{
    private readonly IAnalysisProvider _inner;
    private readonly IBasicAnalysisStore _store;

    public DbAnalysisProvider(IAnalysisProvider inner, IBasicAnalysisStore store)
    {
        _inner = inner;
        _store = store;
    }

    public async Task<BasicAnalysis> GetAsync(DecodedTrack track, CancellationToken ct = default)
    {
        var hit = await _store.TryGetAsync(track.FilePath);
        if (hit is not null)
            return hit;

        var computed = await _inner.GetAsync(track, ct);
        await WriteBackAsync(track.FilePath, computed);
        return computed;
    }

    /// <summary>
    /// Force a fresh compute, bypassing every cache (this one included), and
    /// overwrite the store entry with the result. This is the "bypass and write
    /// through" verb, and it belongs here explicitly — it's used by the "hold
    /// song-select to re-analyze" gesture when the cached BPM/beats are wrong.
    /// </summary>
    public async Task<BasicAnalysis> RecomputeAsync(DecodedTrack track, CancellationToken ct = default)
    {
        var computed = await _inner.RecomputeAsync(track, ct);
        await WriteBackAsync(track.FilePath, computed);
        return computed;
    }

    /// <summary>
    /// Write-through to the store, swallowing failures the same way
    /// <see cref="AnalysisProvider"/>'s tier walk always has — a failed write here
    /// should not fail the lookup or the recompute, it should just mean the next
    /// lookup misses the DB tier again too.
    /// </summary>
    private async Task WriteBackAsync(string path, BasicAnalysis analysis)
    {
        try { await _store.PutAsync(path, analysis); }
        catch (Exception ex)
        {
            var chain = ex.Message;
            for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
                chain += $"  ->  {inner.GetType().Name}: {inner.Message}";
            Console.WriteLine($"[AnalysisProvider] backfill to {_store.Name} failed: {chain}");
        }
    }
}
