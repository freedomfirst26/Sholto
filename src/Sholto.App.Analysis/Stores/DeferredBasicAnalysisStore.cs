using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Analysis.Stores;

/// <summary>
/// Forwards to the real <see cref="IBasicAnalysisStore"/> once the library database has
/// opened; every call awaits <paramref name="store"/> first. A database that failed to
/// open (the task yields null) means no persistence: reads miss, writes go nowhere.
/// </summary>
public sealed class DeferredBasicAnalysisStore(Task<IBasicAnalysisStore?> store) : IBasicAnalysisStore
{
    private readonly Task<IBasicAnalysisStore?> _store = store;

    // Only read for the DbAnalysisProvider backfill-failure log line, so a fixed label is enough.
    public string Name => "database";

    public async Task<BasicAnalysis?> TryGetAsync(string filePath)
    {
        var s = await _store;
        return s is null ? null : await s.TryGetAsync(filePath);
    }

    public async Task PutAsync(string filePath, BasicAnalysis analysis)
    {
        var s = await _store;
        if (s is not null) await s.PutAsync(filePath, analysis);
    }

    public async Task<IReadOnlyDictionary<string, double>> GetDetectedBpmsAsync()
    {
        var s = await _store;
        return s is null ? new Dictionary<string, double>() : await s.GetDetectedBpmsAsync();
    }
}
