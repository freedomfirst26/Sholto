using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools.Tests;

/// <summary><see cref="IExternalTool"/> fake that plays the part of a <c>beat_this</c> process:
/// it makes the work directory (as <see cref="ConfiguredTool"/> does), writes
/// <paramref name="beatsFile"/> (when not null) to the path the definition passed after
/// <c>-o</c>, then hands <paramref name="exitCode"/> to the definition's own
/// <see cref="IToolDefinition{TResult}.Verify"/>. Records what it was asked and what came back.</summary>
public sealed class BeatsFileReplayTool(string? beatsFile, int exitCode = 0) : IExternalTool
{
    private readonly string? _beatsFile = beatsFile;
    private readonly int _exitCode = exitCode;

    public bool IsAvailable => true;
    public IReadOnlyList<string> Args { get; private set; } = [];
    public string WorkDir { get; private set; } = "";
    public object? Outcome { get; private set; }

    public Task<ToolOutcome<T>> RunAsync<T>(
        IToolDefinition<T> tool, ToolInput toolInput, IAnalysisReporter reporter, CancellationToken ct)
    {
        Directory.CreateDirectory(toolInput.WorkDir);
        WorkDir = toolInput.WorkDir;
        Args = tool.BuildArgs(toolInput);
        if (_beatsFile is not null)
            File.WriteAllText(Args[Args.ToList().IndexOf("-o") + 1], _beatsFile);
        var outcome = tool.Verify(toolInput, stdout: "", _exitCode);
        Outcome = outcome;
        return Task.FromResult(outcome);
    }
}
