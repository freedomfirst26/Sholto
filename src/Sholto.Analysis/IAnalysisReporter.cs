using Sholto.Analysis.Reporting;

namespace Sholto.Analysis;

/// <summary>
/// Port for <see cref="AnalysisReporter"/>. <c>Sholto.ExternalTools</c> used to
/// declare its own structurally-identical <c>IToolProgress</c> shape rather than
/// depend on this one, because at the time <c>Sholto.ExternalTools</c> had zero
/// project references and <c>Sholto.Analysis</c> depended on it, not the other
/// way round. That dependency has since been inverted (<c>Sholto.ExternalTools</c>
/// now references <c>Sholto.Analysis</c>), which made <c>IToolProgress</c> and its
/// adapter pointless — every external-tool call site takes this port directly now,
/// and <c>IToolProgress</c> has been deleted.
///
/// Carries the whole public surface of <see cref="AnalysisReporter"/>: the three
/// progress calls every analysis step reports through, plus <see cref="ReportFor"/>
/// and <see cref="Updated"/>, which <c>MainViewModel</c> uses to subscribe to
/// per-track progress.
/// </summary>
public interface IAnalysisReporter
{
    /// <summary>Raised whenever a report's status flips or progresses.</summary>
    event Action<AnalysisReport>? Updated;

    /// <summary>The (possibly still-empty) report for one track, creating it on first ask.</summary>
    AnalysisReport ReportFor(string filePath);

    /// <summary>The step is running, at fraction <paramref name="progress"/> (0..1).</summary>
    void Running(string filePath, string stepName, double progress = 0, string? message = null);

    /// <summary>The step finished successfully.</summary>
    void Complete(string filePath, string stepName, string? message = null);

    /// <summary>The step failed with <paramref name="message"/>.</summary>
    void Failed(string filePath, string stepName, string message);
}
