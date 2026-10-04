using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio.Golden;

/// <summary>An unavailable stem step, the same shape as Bench's internal
/// NoOpStemAnalysisStep (which this project cannot see). The golden scenarios load
/// samples through <see cref="Deck.Load"/>, which never reaches the stem stage.</summary>
internal sealed class NoStemAnalysisStep : IStemAnalysisStep
{
    public string StepName => AnalysisSteps.Stems;
    public bool IsAvailable => false;

    public Task<StemPaths> AnalyzeAsync(string filePath, IAnalysisReporter reporter, CancellationToken ct = default) =>
        throw new NotSupportedException("The golden scenarios load via Deck.Load, which never calls the stem stage.");
}
