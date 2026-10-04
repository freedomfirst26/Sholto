using Sholto.App.Analysis.Analyzers.Keys;

namespace Sholto.App.Analysis.Stores;

/// <summary>
/// Forwards to the real <see cref="IKeyAnalysisStore"/> once the library database has
/// opened; every call awaits <paramref name="store"/> first. A database that failed to
/// open (the task yields null) means no persistence: reads miss, writes go nowhere.
/// </summary>
public sealed class DeferredKeyAnalysisStore(Task<IKeyAnalysisStore?> store) : IKeyAnalysisStore
{
    private readonly Task<IKeyAnalysisStore?> _store = store;

    public async Task<KeyAnalysis?> TryGetAsync(string filePath)
    {
        var s = await _store;
        return s is null ? null : await s.TryGetAsync(filePath);
    }

    public async Task PutAsync(string filePath, KeyAnalysis key)
    {
        var s = await _store;
        if (s is not null) await s.PutAsync(filePath, key);
    }

    public async Task<IReadOnlyDictionary<string, KeyAnalysis>> GetAllAsync()
    {
        var s = await _store;
        return s is null ? new Dictionary<string, KeyAnalysis>() : await s.GetAllAsync();
    }
}
