using Sholto.Analysis.Reporting;

namespace Sholto.Analysis;

/// <summary>
/// A descriptor for one arm's-length external CLI tool (madmom, demucs).
/// The process boundary is load-bearing for the BUSL position and stays — this does
/// not pull the tools in-process, it just declares the contract every call site was
/// previously reinventing on its own: what to run, and how to tell whether it
/// actually worked.
///
/// The governing rule: exit code 0 is not success. <see cref="Verify"/> is the
/// postcondition check, and it is the ONLY way to produce a <see cref="ToolOutcome{T}"/>
/// — there is no path from "process exited" to "Complete" that skips it.
/// </summary>
public interface IToolDefinition<TResult>
{
    /// <summary>Step name used by the <see cref="AnalysisReporter"/> (e.g. "beats",
    /// "stems", "segments").</summary>
    string Name { get; }

    /// <summary>The binary name (e.g. "demucs") — used for log/error messages and as
    /// the key a caller locates via <see cref="ExternalToolFinder.Locate"/> at
    /// bootstrap, before the resolved path is handed to <see cref="ExternalToolRunner.RunAsync"/>.</summary>
    string BinaryName { get; }

    /// <summary>True if analysis cannot proceed without this tool (madmom). False if
    /// its absence just means a graceful degrade (demucs).</summary>
    bool IsRequired { get; }

    /// <summary>Build argv for the process — never a shell string; filenames are
    /// arbitrary user data.</summary>
    IReadOnlyList<string> BuildArgs(ToolInput toolInput);

    /// <summary>
    /// The postcondition check. Called once the process has exited, with its exit
    /// code and full captured stdout. Implementations decide for themselves how exit
    /// code and on-disk/parsed state combine into success or failure — and are
    /// expected to give a different reason for "exited non-zero" than for "exited 0
    /// but did not actually produce its output" (the distinction the 5 Sep 2026
    /// demucs incident showed matters).
    /// </summary>
    ToolOutcome<TResult> Verify(ToolInput toolInput, string stdout, int exitCode);

    /// <summary>
    /// Try to read a progress fraction (0..1) out of one output line. Optional —
    /// tools with no progress reporting just never match. This is intentionally NOT
    /// exclusive with the line being kept in the failure tail: a line can be both
    /// progress AND retained evidence. The runner enforces that; this only parses.
    /// </summary>
    bool TryParseProgress(string line, out double progress)
    {
        progress = 0;
        return false;
    }
}
