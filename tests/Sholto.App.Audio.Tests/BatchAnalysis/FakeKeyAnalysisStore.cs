using System.Collections.Concurrent;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stores;

namespace Sholto.App.Audio.Tests.BatchAnalysis;

/// <summary>An in-memory <see cref="IKeyAnalysisStore"/>.</summary>
internal sealed class FakeKeyAnalysisStore : IKeyAnalysisStore
{
    private readonly ConcurrentDictionary<string, KeyAnalysis> _stored = new();

    public void Store(string path) => _stored[path] = new KeyAnalysis(null);
    public bool Has(string path) => _stored.ContainsKey(path);

    public Task<KeyAnalysis?> TryGetAsync(string filePath) => Task.FromResult(_stored.GetValueOrDefault(filePath));
    public Task PutAsync(string filePath, KeyAnalysis key) { _stored[filePath] = key; return Task.CompletedTask; }
    public Task<IReadOnlyDictionary<string, KeyAnalysis>> GetAllAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, KeyAnalysis>>(new Dictionary<string, KeyAnalysis>(_stored));
}
