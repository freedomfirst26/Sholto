using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.TestSupport;

/// <summary>
/// Minimal <see cref="IExternalTool"/> fake for tests that need to construct a
/// tool-backed analysis step but never actually invoke the tool: no binary, and a
/// <see cref="RunAsync{T}"/> that throws if a test accidentally calls it.
/// </summary>
public sealed class NullExternalTool : IExternalTool
{
    public bool IsAvailable => false;

    public Task<ToolOutcome<T>> RunAsync<T>(
        IToolDefinition<T> tool, ToolInput toolInput, IAnalysisReporter reporter, CancellationToken ct) =>
        throw new NotImplementedException("NullExternalTool is never meant to run a tool.");
}
