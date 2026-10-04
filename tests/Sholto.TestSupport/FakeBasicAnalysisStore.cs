using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Stores;

namespace Sholto.TestSupport;

/// <summary>Only the detected-BPM listing a scan hydrates from is real; the rest is unused.</summary>
internal sealed class FakeBasicAnalysisStore(IReadOnlyDictionary<string, double> bpms) : IBasicAnalysisStore
{
    private readonly IReadOnlyDictionary<string, double> _bpms = bpms;

    public string Name => "fake";

    public Task<BasicAnalysis?> TryGetAsync(string filePath) => Task.FromResult<BasicAnalysis?>(null);

    public Task PutAsync(string filePath, BasicAnalysis analysis) => Task.CompletedTask;

    public Task<IReadOnlyDictionary<string, double>> GetDetectedBpmsAsync() => Task.FromResult(_bpms);
}
