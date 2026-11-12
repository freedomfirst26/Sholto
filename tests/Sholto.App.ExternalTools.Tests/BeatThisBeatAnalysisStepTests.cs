using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools.Tests;

/// <summary>The Beat This! step, reached through its public factory with a fake process that
/// writes canned <c>beat_this</c> output where the step asked for it.</summary>
public sealed class BeatThisBeatAnalysisStepTests : IDisposable
{
    // Real beat_this output shape: "TIME\tBEAT_NUMBER", 1 = downbeat. 120 BPM, 4/4, two bars.
    private const string TwoBarsAt120 =
        "0.5\t1\n1.0\t2\n1.5\t3\n2.0\t4\n2.5\t1\n3.0\t2\n3.5\t3\n4.0\t4\n";

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"sholto_beat_this_test_{Guid.NewGuid():N}");

    public BeatThisBeatAnalysisStepTests() => Directory.CreateDirectory(_tempRoot);

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private IBeatAnalysisStep Step(IExternalTool tool) =>
        new BeatThisBeatAnalysisStepFactory(new ToolOutcomeFactory(), new DetectedBeatsFactory(), _tempRoot)
            .Create(tool, new NullAnalysisReporter());

    private ToolOutcome<DetectedBeats> OutcomeOf(BeatsFileReplayTool tool) => (ToolOutcome<DetectedBeats>)tool.Outcome!;

    [Fact]
    public async Task Every_line_is_a_beat_beat_number_1_is_a_downbeat_and_bpm_is_the_trimmed_mean()
    {
        var tool = new BeatsFileReplayTool(TwoBarsAt120);

        var beats = await Step(tool).AnalyzeAsync("/music/track.mp3");

        Assert.Equal([0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0], beats.BeatTimes);
        Assert.Equal([0.5, 2.5], beats.DownbeatTimes);
        Assert.Equal(120.0, beats.Bpm);
        Assert.True(OutcomeOf(tool).IsSuccess);
    }

    [Fact]
    public async Task Runs_on_the_cpu_with_the_default_postprocessor_writing_into_a_fresh_dir_under_the_temp_root()
    {
        var tool = new BeatsFileReplayTool(TwoBarsAt120);

        await Step(tool).AnalyzeAsync("/music/track.mp3");

        Assert.Equal("/music/track.mp3", tool.Args[0]);
        Assert.Equal(Path.Combine(tool.WorkDir, "track.beats"), tool.Args[tool.Args.ToList().IndexOf("-o") + 1]);
        Assert.Equal(_tempRoot, Path.GetDirectoryName(tool.WorkDir));
        Assert.Equal("-1", tool.Args[tool.Args.ToList().IndexOf("--gpu") + 1]);
        Assert.DoesNotContain("--dbn", tool.Args);
    }

    [Fact]
    public async Task The_work_dir_is_deleted_after_the_run()
    {
        var tool = new BeatsFileReplayTool(TwoBarsAt120);

        await Step(tool).AnalyzeAsync("/music/track.mp3");

        Assert.False(Directory.Exists(tool.WorkDir));
    }

    [Fact]
    public async Task A_non_zero_exit_is_a_failure_with_the_exit_code_and_the_work_dir_is_still_deleted()
    {
        var tool = new BeatsFileReplayTool(TwoBarsAt120, exitCode: 2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Step(tool).AnalyzeAsync("/music/track.mp3"));

        Assert.False(OutcomeOf(tool).IsSuccess);
        Assert.Equal("beat_this exited with code 2", OutcomeOf(tool).Reason);
        Assert.Equal("beat_this exited with code 2", ex.Message);
        Assert.False(Directory.Exists(tool.WorkDir));
    }

    [Fact]
    public async Task Zero_beats_is_a_failure_with_a_reason()
    {
        var tool = new BeatsFileReplayTool("");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Step(tool).AnalyzeAsync("/music/track.mp3"));

        Assert.False(OutcomeOf(tool).IsSuccess);
        Assert.Equal("beat_this exited 0 but produced no beats", OutcomeOf(tool).Reason);
    }

    [Fact]
    public async Task No_beats_file_is_a_failure_with_a_reason()
    {
        var tool = new BeatsFileReplayTool(beatsFile: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Step(tool).AnalyzeAsync("/music/track.mp3"));

        Assert.False(OutcomeOf(tool).IsSuccess);
        Assert.Equal("beat_this exited 0 but wrote no beats file", OutcomeOf(tool).Reason);
    }

    [Fact]
    public async Task A_missing_beat_this_names_its_install_command()
    {
        var missing = new ConfiguredTool(new ExternalToolRunner(new ToolOutcomeFactory()), binaryPath: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Step(missing).AnalyzeAsync("/music/track.mp3"));

        Assert.Contains(BeatThisBeatAnalysisStep.InstallCommand, ex.Message);
    }
}
