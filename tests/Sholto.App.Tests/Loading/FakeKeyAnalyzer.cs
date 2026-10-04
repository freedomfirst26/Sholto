using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Reporting;

namespace Sholto.App.Tests;

/// <summary>Always finds the key it was given.</summary>
internal sealed class FakeKeyAnalyzer(Key? key) : IKeyAnalyzer
{
    private readonly Key? _key = key;

    public Task<KeyAnalysis> AnalyzeAsync(DecodedTrack track, IAnalysisReporter reporter, CancellationToken ct = default) =>
        Task.FromResult(new KeyAnalysis(_key));
}
