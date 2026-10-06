using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Stores;

namespace Sholto.App.Analysis.Tests;

/// <summary>A basic-analysis store that answers every lookup with one scripted value and records its traffic.</summary>
internal sealed class ScriptedBasicAnalysisStore(BasicAnalysis? stored) : IBasicAnalysisStore
{
    private readonly BasicAnalysis? _stored = stored;

    public string Name => "scripted";
    public int Lookups { get; private set; }
    public List<string> Puts { get; } = [];

    public Task<BasicAnalysis?> TryGetAsync(string filePath)
    {
        Lookups++;
        return Task.FromResult(_stored);
    }

    public Task PutAsync(string filePath, BasicAnalysis analysis)
    {
        Puts.Add(filePath);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, double>> GetDetectedBpmsAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double>());
}
