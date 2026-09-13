using Sholto.Analysis.Reporting;

namespace Sholto.Analysis.ToolBoundary;

/// <summary>
/// One tool (madmom / demucs / ...), fully configured and ready to run —
/// the single collaborator an analyser needs. Replaces the loose
/// <c>(IExternalToolRunner runner, string? binaryPath, string cacheRoot, ...)</c>
/// primitives every analyser used to take and then do its own
/// <c>Path.Combine</c>/<c>Environment.GetFolderPath</c> work with.
///
/// Deliberately does NOT know where a run should write its output
/// (<c>WorkspaceFor</c> used to live here) — that was cache/output-layout policy
/// leaking into a port that is only about running a tool. Callers that need a
/// deterministic work directory (e.g. the on-disk stem cache) own that policy
/// themselves now and pass it to <see cref="RunAsync{T}"/> explicitly; a caller
/// with no such need (madmom) just passes the input file's own directory.
///
/// Built and handed out by <see cref="ToolSet"/>; analysers never construct
/// one themselves.
/// </summary>
public interface IExternalTool
{
    bool IsAvailable { get; }

    /// <summary>Run <paramref name="tool"/> against <paramref name="toolInput"/>.
    /// Ensures <see cref="ToolInput.WorkDir"/> exists, then delegates to the
    /// underlying <see cref="IExternalToolRunner"/> with the resolved binary path —
    /// the analyser supplies the tool descriptor and the input file / work
    /// directory pair.</summary>
    Task<ToolOutcome<T>> RunAsync<T>(
        IToolDefinition<T> tool, ToolInput toolInput, IAnalysisReporter reporter, CancellationToken ct);
}
