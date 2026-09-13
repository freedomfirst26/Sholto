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

/// <summary>The do-nothing reporter — every call is a no-op, <see cref="Updated"/>
/// is never raised, and <see cref="ReportFor"/> hands back a shared, permanently
/// empty <see cref="AnalysisReport"/>. Exists so the 9 analysis entry points that
/// used to accept <c>IAnalysisReporter? reporter = null</c> can default to a real
/// object instead of null — nobody in the app ever actually wants silence, but a
/// Bench harness or a lower-level unit test that doesn't care about progress
/// shouldn't have to construct a real <see cref="AnalysisReporter"/> just to have
/// something to pass. Follows the same convention as <see cref="NullKeyAnalysisStore"/>.</summary>
public sealed class NullAnalysisReporter : IAnalysisReporter
{
    public static readonly NullAnalysisReporter Instance = new();
    private NullAnalysisReporter() { }

    private static readonly AnalysisReport EmptyReport = new(string.Empty, Array.Empty<string>());

    public event Action<AnalysisReport>? Updated { add { } remove { } }

    public AnalysisReport ReportFor(string filePath) => EmptyReport;
    public void Running(string filePath, string stepName, double progress = 0, string? message = null) { }
    public void Complete(string filePath, string stepName, string? message = null) { }
    public void Failed(string filePath, string stepName, string message) { }
}
