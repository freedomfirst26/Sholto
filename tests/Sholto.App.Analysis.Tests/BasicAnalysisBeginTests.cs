using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stages;

namespace Sholto.App.Analysis.Tests;

public class BasicAnalysisBeginTests
{
    private const string Path = "/music/a.mp3";
    private const int SpinMs = 5000;

    private BasicAnalysisStage MakeStage(GatedBeatStep beats) => new(
        beats,
        new RecordingPeakAnalyzer(() => beats.Started),
        new BeatgridAnalyzer(new BeatgridFactory()),
        new AnalysisReporter(Array.Empty<string>()));

    private DecodedTrack Track() => new(Path, new float[4096], 48000, 2);

    [Fact]
    public async Task Begin_starts_the_beat_step_before_any_samples_exist()
    {
        var beats = new GatedBeatStep();
        var stage = MakeStage(beats);

        var request = stage.Begin(Path);

        Assert.True(SpinWait.SpinUntil(() => beats.Started, SpinMs), "the beat step never started");
        beats.Complete();
        var basic = await request.CompleteAsync(Track());

        Assert.Equal(120, basic.Bpm);
    }

    [Fact]
    public async Task A_stored_analysis_is_returned_without_starting_the_beat_step()
    {
        var beats = new GatedBeatStep();
        var stored = new BasicAnalysis(null!, 99, [0.0], [0.0]);
        var store = new ScriptedBasicAnalysisStore(stored);
        var provider = new DbAnalysisProvider(new StagedAnalysisProvider(MakeStage(beats)), store);

        var request = provider.Begin(Path);
        var basic = await request.CompleteAsync(Track());

        Assert.Same(stored, basic);
        Assert.False(beats.Started);
    }

    [Fact]
    public async Task A_miss_starts_the_beat_step_before_the_track_is_decoded_and_writes_the_result_back()
    {
        var beats = new GatedBeatStep();
        var store = new ScriptedBasicAnalysisStore(null);
        var provider = new DbAnalysisProvider(new StagedAnalysisProvider(MakeStage(beats)), store);

        var request = provider.Begin(Path);

        Assert.True(SpinWait.SpinUntil(() => beats.Started, SpinMs), "the beat step never started on a miss");
        beats.Complete();
        var basic = await request.CompleteAsync(Track());

        Assert.Equal(120, basic.Bpm);
        Assert.Equal([Path], store.Puts);
    }

    [Fact]
    public async Task The_memory_tier_answers_a_second_begin_without_the_store()
    {
        var beats = new GatedBeatStep();
        var store = new ScriptedBasicAnalysisStore(null);
        var provider = new CachingAnalysisProvider(
            new DbAnalysisProvider(new StagedAnalysisProvider(MakeStage(beats)), store));

        var first = provider.Begin(Path);
        Assert.True(SpinWait.SpinUntil(() => beats.Started, SpinMs), "the beat step never started");
        beats.Complete();
        var firstResult = await first.CompleteAsync(Track());

        var second = await provider.Begin(Path).CompleteAsync(Track());

        Assert.Equal(120, firstResult.Bpm);
        Assert.Equal(120, second.Bpm);
        Assert.Equal(1, store.Lookups);
    }
}
