namespace Sholto.Analysis.Reporting;

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
