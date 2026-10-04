using Sholto.App.Analysis;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;

namespace Sholto.Interface.Bench.Rendering;

/// <summary>Implements <see cref="IStemAnalysisStep"/>, needed by <c>Deck</c>'s
/// constructor. The stem-presence role is a separate concrete
/// <see cref="DemucsStemPresence"/> instance (the app's <c>CachingStemAnalysisStep</c> shows why the
/// two roles are split); this no-op only covers the analysis-step role, since it's never actually
/// called here.</summary>
public sealed class NoOpStemAnalysisStep : IStemAnalysisStep
{
    public string StepName => AnalysisSteps.Stems;
    public bool IsAvailable => false;
    public Task<StemPaths> AnalyzeAsync(string filePath, IAnalysisReporter reporter, CancellationToken ct = default) =>
        throw new NotSupportedException("Sholto.Interface.Bench renders via Deck.LoadStreaming, which never calls the StemAnalysisStage.");
}
