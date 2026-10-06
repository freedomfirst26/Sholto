using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stages;

namespace Sholto.App.Analysis.Tests;

public class BasicAnalysisStageFailureTests
{
    [Fact]
    public async Task A_peaks_failure_marks_Waveform_Failed_and_leaves_Beats_alone()
    {
        const string path = "/music/a.mp3";
        var beats = new GatedBeatStep();
        var reporter = new AnalysisReporter(Array.Empty<string>());
        var stage = new BasicAnalysisStage(
            beats, new ThrowingPeakAnalyzer("peaks boom"), new BeatgridAnalyzer(new BeatgridFactory()), reporter);

        var run = stage.RunAsync(new DecodedTrack(path, new float[4096], 48000, 2));
        Assert.True(SpinWait.SpinUntil(() => beats.Started, 5000), "the beat step never started");
        beats.Complete();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
        Assert.Equal("peaks boom", ex.Message);

        var steps = reporter.ReportFor(path).Steps;
        Assert.Equal(AnalysisState.Failed, steps[AnalysisSteps.Waveform].State);
        Assert.Equal("peaks boom", steps[AnalysisSteps.Waveform].Message);
        Assert.NotEqual(AnalysisState.Failed, steps[AnalysisSteps.Beats].State);
    }
}
