using Sholto.Analysis.Reporting;
using Sholto.Analysis.Stems;

namespace Sholto.Analysis.Processing;

/// <summary>
/// Decorator: caching is not <see cref="IStemAnalysisStep"/>'s concern, so it lives
/// here instead of inside <see cref="DemucsStemAnalysisStep"/> (which now runs demucs
/// unconditionally, every time it's asked). Hit → return the cached
/// <see cref="StemPaths"/> without ever touching the inner step; miss → delegate to
/// it (a real demucs run, 30-180 s).
///
/// The inner step and the cache agree on where a track's stems live because the
/// composition root hands both the same <c>workspaceFor</c> function — see
/// <see cref="DemucsStemPresence"/>'s own comment.
/// </summary>
public sealed class CachingStemAnalysisStep : IStemAnalysisStep
{
    private readonly IStemAnalysisStep _inner;
    private readonly DemucsStemPresence _cache;

    public CachingStemAnalysisStep(IStemAnalysisStep inner, DemucsStemPresence cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public string StepName => _inner.StepName;
    public bool IsAvailable => _inner.IsAvailable;

    public async Task<StemPaths> AnalyzeAsync(
        string filePath, IAnalysisReporter reporter, CancellationToken ct = default)
    {
        var cached = _cache.TryGet(filePath);
        if (cached is not null)
        {
            reporter.Complete(filePath, StepName, "cached");
            return cached;
        }

        return await _inner.AnalyzeAsync(filePath, reporter, ct);
    }
}
