using Sholto.ExternalTools;

namespace Sholto.App.Tests;

/// <summary>
/// Minimal <see cref="IExternalTool"/> fake for tests that need to construct a
/// tool-backed analyser but never actually invoke the tool: no binary, no
/// workspace policy beyond a fixed temp dir, and a <see cref="RunAsync{T}"/> that
/// throws if a test accidentally calls it.
/// </summary>
public sealed class NullExternalTool : IExternalTool
{
    public bool IsAvailable => false;
    public string WorkspaceFor(string inputPath) => "/tmp";

    public Task<ToolOutcome<T>> RunAsync<T>(
        IToolDefinition<T> tool, string input, IToolProgress? reporter, CancellationToken ct) =>
        throw new NotImplementedException("NullExternalTool is never meant to run a tool.");
}
