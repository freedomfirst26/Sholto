using System.Collections.Concurrent;

namespace OpenDJ.Analysis;

/// <summary>Hot in-process cache. The first lookup wins; never invalidates on its own.</summary>
public sealed class MemoryAnalysisCache : IAnalysisCache
{
    private readonly ConcurrentDictionary<string, BasicAnalysis> _byPath = new();
    public string Name => "memory";

    public Task<BasicAnalysis?> TryGetAsync(string filePath)
        => Task.FromResult(_byPath.TryGetValue(filePath, out var a) ? a : null);

    public Task PutAsync(string filePath, BasicAnalysis analysis)
    {
        _byPath[filePath] = analysis;
        return Task.CompletedTask;
    }
}
