using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stages;

namespace Sholto.App.Analysis.Tests;

public class BasicAnalysisStageOverlapTests
{
    [Fact]
    public async Task Waveform_peaks_are_computed_while_the_beat_step_is_still_running()
    {
        var beats = new GatedBeatStep();
        var peaks = new RecordingPeakAnalyzer(() => beats.Started);
        var stage = new BasicAnalysisStage(
            beats, peaks, new BeatgridAnalyzer(new BeatgridFactory()), new AnalysisReporter(Array.Empty<string>()));

        var run = stage.RunAsync(new DecodedTrack("/music/a.mp3", new float[4096], 48000, 2));

        Assert.True(SpinWait.SpinUntil(() => beats.Started, 5000), "the beat step never started");
        Assert.False(run.IsCompleted);
        beats.Complete();
        var basic = await run;

        Assert.True(peaks.OtherWorkRanAlongside,
            "peaks finished before the beat step started; they must run alongside it");
        Assert.Equal(120, basic.Bpm);
    }
}
