using Sholto.App.Analysis.Analyzers.Keys;

namespace Sholto.App.Analysis.Stores;

/// <summary>The do-nothing store — always a miss, writes go nowhere. Used while
/// there is no database (e.g. the Bench harness).</summary>
public sealed class NullKeyAnalysisStore : IKeyAnalysisStore
{
    public Task<KeyAnalysis?> TryGetAsync(string filePath) => Task.FromResult<KeyAnalysis?>(null);
    public Task PutAsync(string filePath, KeyAnalysis key) => Task.CompletedTask;
    public Task<IReadOnlyDictionary<string, KeyAnalysis>> GetAllAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, KeyAnalysis>>(new Dictionary<string, KeyAnalysis>());
}
