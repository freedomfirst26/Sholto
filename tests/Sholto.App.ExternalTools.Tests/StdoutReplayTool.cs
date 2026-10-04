using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools.Tests;

/// <summary><see cref="IExternalTool"/> fake that plays the part of a process which
/// exited 0 after printing <paramref name="stdout"/>: it hands that text straight to
/// the tool definition's own <see cref="IToolDefinition{TResult}.Verify"/>. Lets a test
/// reach a definition's parsing and post-processing through the public analysis step,
/// without a binary.</summary>
public sealed class StdoutReplayTool(string stdout) : IExternalTool
{
    private readonly string _stdout = stdout;

    public bool IsAvailable => true;

    public Task<ToolOutcome<T>> RunAsync<T>(
        IToolDefinition<T> tool, ToolInput toolInput, IAnalysisReporter reporter, CancellationToken ct) =>
        Task.FromResult(tool.Verify(toolInput, _stdout, exitCode: 0));
}
