using System.Diagnostics;
using System.Text;

using Sholto.Analysis;
using Sholto.Analysis.Reporting;
using Sholto.Analysis.ToolBoundary;

namespace Sholto.ExternalTools;

/// <summary>
/// Owns everything the individual external-tool call sites used to duplicate:
/// resolve the binary, start the process, pump both output streams, wait for exit,
/// then hand the exit code and captured stdout to the tool's own
/// <see cref="IToolDefinition{TResult}.Verify"/> and report Complete/Failed accordingly.
///
/// Every line — stdout or stderr — goes into the failure tail unconditionally.
/// Progress parsing is a separate, non-exclusive pass over the same line: a line
/// can drive the progress chip AND still be retained as failure evidence. This is
/// the structural fix for the bug where any line containing a '%' (e.g. "Only 50% of
/// the checkpoint weights were loaded") was consumed as progress and silently lost.
///
/// Cache short-circuiting is deliberately NOT here — it lives in a decorator in front
/// of the analysis step that needs it (e.g. <c>CachingStemAnalysisStep</c> in
/// <c>Sholto.Analysis</c>), so a cache hit costs nothing beyond a filesystem stat and
/// never touches the process machinery, and this runner stays unaware caching exists.
/// </summary>
public sealed class ExternalToolRunner : IExternalToolRunner
{
    public async Task<ToolOutcome<TResult>> RunAsync<TResult>(
        IToolDefinition<TResult> tool,
        string? binaryPath,
        ToolInput toolInput,
        IAnalysisReporter reporter,
        CancellationToken ct = default)
    {
        var input = toolInput.Input;

        if (binaryPath is null)
        {
            var reason = $"{tool.BinaryName} not found";
            reporter.Failed(input, tool.Name, reason);
            return ToolOutcome<TResult>.Failure(reason);
        }

        reporter.Running(input, tool.Name, 0, $"{tool.BinaryName} starting");

        var psi = new ProcessStartInfo(binaryPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in tool.BuildArgs(toolInput))
            psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var tail = new ProcessOutputTail();
        var stdoutBuf = new StringBuilder();
        var gate = new object();

        void OnLine(string? line, bool isStdout)
        {
            if (line is null) return;
            if (isStdout)
            {
                lock (gate) stdoutBuf.Append(line).Append('\n');
            }

            // EVERY line goes to the tail unconditionally — this is the fix. Progress
            // parsing below is separate and non-exclusive with retaining the line.
            tail.Add(line);

            if (tool.TryParseProgress(line, out var progress))
                reporter.Running(input, tool.Name, Math.Clamp(progress, 0.0, 1.0), line.Trim());
        }

        proc.OutputDataReceived += (_, e) => OnLine(e.Data, isStdout: true);
        proc.ErrorDataReceived += (_, e) => OnLine(e.Data, isStdout: false);

        bool started;
        try
        {
            started = proc.Start();
        }
        catch (Exception ex)
        {
            var reason = $"could not start {tool.BinaryName}: {ex.Message}";
            reporter.Failed(input, tool.Name, reason);
            return ToolOutcome<TResult>.Failure(reason);
        }

        if (!started)
        {
            var reason = $"could not start {tool.BinaryName}";
            reporter.Failed(input, tool.Name, reason);
            return ToolOutcome<TResult>.Failure(reason);
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct);

        string stdout;
        lock (gate) stdout = stdoutBuf.ToString();

        var outcome = tool.Verify(toolInput, stdout, proc.ExitCode);
        if (outcome.IsSuccess)
        {
            reporter.Complete(input, tool.Name, outcome.Message ?? "ok");
            return outcome;
        }

        var msg = tail.Annotate(outcome.Reason ?? $"{tool.BinaryName} failed");
        reporter.Failed(input, tool.Name, msg);
        return ToolOutcome<TResult>.Failure(msg);
    }
}
