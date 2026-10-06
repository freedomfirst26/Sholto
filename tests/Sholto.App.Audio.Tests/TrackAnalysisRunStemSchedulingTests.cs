using Sholto.App.Analysis.Analyzers.Keys;

namespace Sholto.App.Audio.Tests;

public class TrackAnalysisRunStemSchedulingTests
{
    private const string TrackA = "/music/a.mp3";

    [Fact]
    public async Task On_cuda_stems_start_before_basic_analysis_completes()
    {
        var rig = new TrackAnalysisRunRig(stemsOverlap: true);

        rig.Load(TrackA);

        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.StemsRequested(TrackA), 2000));
        Assert.True(rig.Pipeline.BasicRequested(TrackA));
        rig.Pipeline.CompleteAll(TrackA, bpm: 120);
        await rig.Settle();
        Assert.Single(rig.DetectedBasic);
        Assert.Single(rig.StemsReady);
    }

    [Fact]
    public async Task On_cpu_stems_start_only_after_basic_analysis_completes()
    {
        var rig = new TrackAnalysisRunRig(stemsOverlap: false);

        rig.Load(TrackA);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.BasicRequested(TrackA), 2000));
        await Task.Delay(300);
        Assert.False(rig.Pipeline.StemsRequested(TrackA));

        rig.Pipeline.CompleteBasicAndKey(TrackA, bpm: 120);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.StemsRequested(TrackA), 2000));
        await rig.Settle();
    }

    [Fact]
    public async Task On_cuda_a_superseded_load_still_drops_the_old_tracks_results_and_cancels_its_stems()
    {
        const string trackB = "/music/b.mp3";
        var rig = new TrackAnalysisRunRig(stemsOverlap: true);
        rig.Load(TrackA);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.StemsRequested(TrackA), 2000));

        rig.Load(trackB);
        rig.Pipeline.CompleteAll(TrackA, bpm: 123);
        await rig.Settle();

        Assert.True(rig.Pipeline.StemToken(TrackA).IsCancellationRequested);
        Assert.Empty(rig.DetectedBasic);
        Assert.Empty(rig.StemsReady);
    }

    [Fact]
    public async Task The_key_is_applied_as_soon_as_it_is_ready_without_waiting_for_the_beat_grid()
    {
        var rig = new TrackAnalysisRunRig();
        rig.Load(TrackA);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.BasicRequested(TrackA), 2000));

        rig.Pipeline.CompleteKey(TrackA);
        await rig.Settle();

        Assert.NotNull(rig.Run.Analysis.Get<KeyAnalysis>());
        Assert.Null(rig.Run.Analysis.Basic);
        Assert.Empty(rig.DetectedBasic);
        rig.Pipeline.CompleteBasic(TrackA, bpm: 120);
        await rig.Settle();
    }
}
