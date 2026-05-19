namespace Sholto.Analysis;

/// <summary>
/// Layered cache-aside lookup for BasicAnalysis. Caches are checked
/// fastest-first; on a miss we fall through to the next tier and finally
/// to compute. Every hit lower in the stack is back-filled into the tiers
/// above it so the next lookup is faster.
///
/// Typical wiring:
///   new AnalysisProvider(
///       caches: [ new MemoryAnalysisCache(), new DatabaseAnalysisCache(db) ],
///       compute: (path, samples, rate) => BasicAnalysis.ComputeAsync(...));
/// </summary>
public sealed class AnalysisProvider
{
    private readonly IReadOnlyList<IAnalysisCache> _caches;
    private readonly Func<string, float[], int, CancellationToken, Task<BasicAnalysis>> _compute;

    public AnalysisProvider(
        IReadOnlyList<IAnalysisCache> caches,
        Func<string, float[], int, CancellationToken, Task<BasicAnalysis>> compute)
    {
        _caches = caches;
        _compute = compute;
    }

    /// <summary>
    /// Resolve the basic analysis for a track. Returns the cache name where the
    /// hit occurred (or "computed") so callers can log/observe cache behaviour.
    /// </summary>
    public async Task<(BasicAnalysis Analysis, string Source)> GetAsync(
        string filePath, float[] stereoSamples, int sampleRate, CancellationToken ct = default)
    {
        // Walk the tiers fastest → slowest, remember the misses so we can back-fill.
        var misses = new List<IAnalysisCache>();
        foreach (var cache in _caches)
        {
            var hit = await cache.TryGetAsync(filePath);
            if (hit is not null)
            {
                await BackfillAsync(misses, filePath, hit);
                return (hit, cache.Name);
            }
            misses.Add(cache);
        }

        // Full miss — compute and write through to every tier.
        var computed = await _compute(filePath, stereoSamples, sampleRate, ct);
        await BackfillAsync(misses, filePath, computed);
        return (computed, "computed");
    }

    private static async Task BackfillAsync(List<IAnalysisCache> misses, string path, BasicAnalysis analysis)
    {
        foreach (var c in misses)
        {
            try { await c.PutAsync(path, analysis); }
            catch (Exception ex) { Console.WriteLine($"[AnalysisProvider] backfill to {c.Name} failed: {ex.Message}"); }
        }
    }
}
