using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools.Tests;

public class ExternalToolRunnerTests
{
    private readonly ExternalToolRunner _runner = new();
    private readonly string? _shPath;
    private readonly bool _shAvailable;

    public ExternalToolRunnerTests()
    {
        var finder = new ExternalToolFinder(new ExternalToolOptionsFactory(new ExternalToolCatalog()).ForCurrentPlatform());
        _shPath = finder.Locate("sh");
        _shAvailable = _shPath is not null;
    }

    /// <summary>A minimal descriptor that runs a real /bin/sh process so the runner's
    /// process-pumping machinery is exercised end to end.</summary>
    private sealed class ShTool : IToolDefinition<string>
    {
        public required string Script { get; init; }
        public Func<ToolInput, string, int, ToolOutcome<string>>? VerifyOverride { get; init; }

        public string Name => "sh-tool";
        public string BinaryName => "sh";
        public bool IsRequired => false;

        public IReadOnlyList<string> BuildArgs(ToolInput toolInput) => new[] { "-c", Script };

        public ToolOutcome<string> Verify(ToolInput toolInput, string stdout, int exitCode) =>
            VerifyOverride is not null
                ? VerifyOverride(toolInput, stdout, exitCode)
                : (exitCode == 0
                    ? ToolOutcome<string>.Success(stdout)
                    : ToolOutcome<string>.Failure($"exited {exitCode}"));
    }

    [Fact]
    public async Task Exit_zero_with_failing_postcondition_is_reported_Failed_not_Complete()
    {
        if (!_shAvailable) return; // sh should always be present on Linux CI, but guard anyway

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";

        // Exits 0 (echo always succeeds) but Verify always says the postcondition
        // failed — exactly the shape of the 5 Sep 2026 demucs incident.
        var tool = new ShTool
        {
            Script = "echo hello",
            VerifyOverride = (_, _, exitCode) => ToolOutcome<string>.Failure("postcondition not met"),
        };

        var outcome = await _runner.RunAsync(tool, _shPath, new ToolInput(path, "/tmp"), reporter);

        Assert.False(outcome.IsSuccess);
        var step = reporter.ReportFor(path).Steps[tool.Name];
        Assert.Equal(AnalysisState.Failed, step.State);
        Assert.NotEqual(AnalysisState.Complete, step.State);
        Assert.Contains("postcondition not met", step.Message);
    }

    [Fact]
    public async Task A_cancelled_run_is_not_reported_Failed()
    {
        if (!_shAvailable) return;

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";
        var tool = new ShTool { Script = "sleep 30" };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _runner.RunAsync(tool, _shPath, new ToolInput(path, "/tmp"), reporter, cts.Token));

        var report = reporter.ReportFor(path);
        Assert.False(report.HasFailure);
        Assert.False(report.IsBusy);
        if (report.Steps.TryGetValue(tool.Name, out var step))
        {
            Assert.NotEqual(AnalysisState.Failed, step.State);
            Assert.NotEqual(AnalysisState.Running, step.State);
        }
    }

    [Fact]
    public async Task A_diagnostic_line_containing_percent_still_reaches_the_tail()
    {
        if (!_shAvailable) return;

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";

        // A line that contains a '%' but is NOT a progress line (no tqdm-style
        // "NN%|" prefix) must still end up in the failure tail, not be swallowed.
        var tool = new ShTool
        {
            Script = "echo 'Only 50% of the checkpoint weights were loaded'; exit 1",
            VerifyOverride = (_, _, exitCode) => ToolOutcome<string>.Failure($"exited {exitCode}"),
        };

        var outcome = await _runner.RunAsync(tool, _shPath, new ToolInput(path, "/tmp"), reporter);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("Only 50% of the checkpoint weights were loaded", outcome.Reason);
    }

    [Fact]
    public async Task Success_reaches_Complete_with_the_verified_value()
    {
        if (!_shAvailable) return;

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";
        var tool = new ShTool { Script = "echo ok" };

        var outcome = await _runner.RunAsync(tool, _shPath, new ToolInput(path, "/tmp"), reporter);

        Assert.True(outcome.IsSuccess);
        var step = reporter.ReportFor(path).Steps[tool.Name];
        Assert.Equal(AnalysisState.Complete, step.State);
    }

    [Fact]
    public async Task Progress_is_clamped_to_0_and_1()
    {
        if (!_shAvailable) return;

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";

        // A tool whose progress parser reports an out-of-range fraction (e.g. from a
        // 3-digit percentage like "999%") must still end up clamped in the reporter.
        var tool = new ClampingTool();

        await _runner.RunAsync(tool, _shPath, new ToolInput(path, "/tmp"), reporter);

        var step = reporter.ReportFor(path).Steps[tool.Name];
        Assert.InRange(step.Progress, 0.0, 1.0);
    }

    private sealed class ClampingTool : IToolDefinition<string>
    {
        public string Name => "clamp-tool";
        public string BinaryName => "sh";
        public bool IsRequired => false;

        public IReadOnlyList<string> BuildArgs(ToolInput toolInput) =>
            new[] { "-c", "echo '999%|xxx'" };

        public ToolOutcome<string> Verify(ToolInput toolInput, string stdout, int exitCode) =>
            ToolOutcome<string>.Success(stdout);

        public bool TryParseProgress(string line, out double progress)
        {
            if (line.Contains('%'))
            {
                // Deliberately out of range before the runner clamps it.
                progress = 9.99;
                return true;
            }
            progress = 0;
            return false;
        }
    }

    [Fact]
    public async Task Cancelling_the_token_kills_the_process()
    {
        if (!_shAvailable) return;

        var pidFile = Path.Combine(Path.GetTempPath(), $"sholto-kill-{Guid.NewGuid():N}.pid");
        // exec makes the shell's pid the pid of the long-running process itself.
        var tool = new ShTool { Script = $"echo $$ > '{pidFile}'; exec sleep 60" };
        using var cts = new CancellationTokenSource();

        var run = _runner.RunAsync(tool, _shPath, new ToolInput("/music/track.mp3", "/tmp"),
            new AnalysisReporter(Array.Empty<string>()), cts.Token);
        Assert.True(SpinWait.SpinUntil(() => File.Exists(pidFile) && new FileInfo(pidFile).Length > 0, 5000));
        var pid = int.Parse(File.ReadAllText(pidFile).Trim());
        Assert.True(Directory.Exists($"/proc/{pid}"));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        var gone = SpinWait.SpinUntil(() => !Directory.Exists($"/proc/{pid}"), 5000);
        File.Delete(pidFile);
        Assert.True(gone, "the process was still running after its token was cancelled");
    }

    [Fact]
    public async Task Missing_binary_is_reported_Failed_without_starting_a_process()
    {
        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";
        var tool = new MissingBinaryTool();

        var outcome = await _runner.RunAsync(tool, binaryPath: null, new ToolInput(path, "/tmp"), reporter);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("not found", outcome.Reason);
        var step = reporter.ReportFor(path).Steps[tool.Name];
        Assert.Equal(AnalysisState.Failed, step.State);
    }

    private sealed class MissingBinaryTool : IToolDefinition<string>
    {
        public string Name => "missing-tool";
        public string BinaryName => "sholto_definitely_does_not_exist_xyz";
        public bool IsRequired => false;
        public IReadOnlyList<string> BuildArgs(ToolInput toolInput) => Array.Empty<string>();
        public ToolOutcome<string> Verify(ToolInput toolInput, string stdout, int exitCode) =>
            ToolOutcome<string>.Success("unreachable");
    }
}
