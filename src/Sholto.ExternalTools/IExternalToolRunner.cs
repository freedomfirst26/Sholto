using Sholto.Analysis;

namespace Sholto.ExternalTools;

/// <summary>
/// The injection point every analyser depends on instead of the concrete
/// <see cref="ExternalToolRunner"/> — so a fake runner (or a future caching/retrying
/// decorator) can stand in without any analyser knowing.
/// </summary>
public interface IExternalToolRunner
{
    /// <summary>Run <paramref name="tool"/> against the already-resolved
    /// <paramref name="binaryPath"/>. Callers resolve the path once at bootstrap
    /// (see <see cref="ExternalToolFinder"/>) and pass it in — the runner itself
    /// does no lookup. Pass null when the tool wasn't found; the runner reports
    /// failure without starting a process.</summary>
    Task<ToolOutcome<TResult>> RunAsync<TResult>(
        IToolDefinition<TResult> tool,
        string? binaryPath,
        ToolInput toolInput,
        IAnalysisReporter reporter,
        CancellationToken ct = default);
}
