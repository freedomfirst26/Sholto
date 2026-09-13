using Sholto.Analysis;

namespace Sholto.ExternalTools;

/// <summary>
/// Default <see cref="IExternalTool"/>: a resolved binary path plus a workspace policy
/// function, both decided by <see cref="ToolSet"/> at bootstrap. Not
/// constructed directly by anything except the host.
/// </summary>
public sealed class ConfiguredTool : IExternalTool
{
    private readonly IExternalToolRunner _runner;

    public ConfiguredTool(IExternalToolRunner runner, string? binaryPath)
    {
        _runner = runner;
        BinaryPath = binaryPath;
    }

    public string? BinaryPath { get; }
    public bool IsAvailable => BinaryPath is not null;

    public async Task<ToolOutcome<T>> RunAsync<T>(
        IToolDefinition<T> tool, ToolInput toolInput, IAnalysisReporter reporter, CancellationToken ct)
    {
        // Ensure the caller-supplied work directory exists before the process runs
        // (madmom's "workspace" is just the source file's own directory, which
        // always already exists — this is a no-op for it, and load-bearing for
        // demucs's cache directory).
        Directory.CreateDirectory(toolInput.WorkDir);
        return await _runner.RunAsync(tool, BinaryPath, toolInput, reporter, ct);
    }
}
