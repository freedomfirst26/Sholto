using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stores;

namespace Sholto.App.Tests;

/// <summary>Remembers every key written through it.</summary>
internal sealed class RecordingKeyAnalysisStore : IKeyAnalysisStore
{
    private readonly object _gate = new();
    private readonly List<(string Path, KeyAnalysis Key)> _puts = [];

    public IReadOnlyList<(string Path, KeyAnalysis Key)> Puts
    {
        get { lock (_gate) return [.. _puts]; }
    }

    public Task<KeyAnalysis?> TryGetAsync(string filePath) => Task.FromResult<KeyAnalysis?>(null);

    public Task PutAsync(string filePath, KeyAnalysis key)
    {
        lock (_gate) _puts.Add((filePath, key));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, KeyAnalysis>> GetAllAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, KeyAnalysis>>(new Dictionary<string, KeyAnalysis>());
}
