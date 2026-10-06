using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio.Tests;

public class TrackAnalysisRunSupersessionTests
{
    private const string TrackA = "/music/a.mp3";
    private const string TrackB = "/music/b.mp3";

    [Fact]
    public async Task Results_of_a_superseded_load_are_not_applied_to_the_new_track()
    {
        var rig = new TrackAnalysisRunRig();
        rig.Load(TrackA);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.BasicRequested(TrackA), 2000));

        rig.Load(TrackB);
        rig.Pipeline.CompleteAll(TrackA, bpm: 123);
        await rig.Settle();

        var analysis = rig.Run.Analysis;
        Assert.Null(analysis.Get<KeyAnalysis>());
        Assert.Null(analysis.Get<StemPaths>());
        Assert.Null(analysis.Basic);
        Assert.Empty(rig.DetectedBasic);
        Assert.Empty(rig.StemsReady);
    }

    [Fact]
    public async Task Loading_a_new_track_cancels_the_stem_run_of_the_old_one()
    {
        var rig = new TrackAnalysisRunRig();
        rig.Load(TrackA);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.BasicRequested(TrackA), 2000));
        rig.Pipeline.CompleteBasicAndKey(TrackA, bpm: 123);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.StemsRequested(TrackA), 2000));
        Assert.False(rig.Pipeline.StemToken(TrackA).IsCancellationRequested);

        rig.Load(TrackB);

        // The token reaches ExternalToolRunner, which kills demucs on it
        // (see ExternalToolRunnerTests.Cancelling_the_token_kills_the_process).
        Assert.True(rig.Pipeline.StemToken(TrackA).IsCancellationRequested);
        await rig.Settle();
    }

    [Fact]
    public async Task Stems_are_applied_on_the_app_thread()
    {
        var rig = new TrackAnalysisRunRig();
        rig.Load(TrackA);
        rig.Pipeline.CompleteAll(TrackA, bpm: 123);

        // Nothing is applied until the app thread gets to it: the pool thread only posts.
        Assert.True(SpinWait.SpinUntil(() => rig.AppThread.Pending >= 4, 2000));
        await Task.Delay(100);
        Assert.Empty(rig.StemsReady);
        Assert.Empty(rig.DetectedBasic);

        await rig.Settle();

        Assert.Single(rig.StemsReady);
        Assert.Equal([true], rig.StemsOnAppThread);
        Assert.Equal([true], rig.BasicOnAppThread);
    }

    [Fact]
    public async Task A_single_load_still_gets_its_stems_key_and_grid()
    {
        var rig = new TrackAnalysisRunRig();
        rig.Load(TrackA);
        rig.Pipeline.CompleteAll(TrackA, bpm: 123);
        Assert.True(SpinWait.SpinUntil(() => { rig.Drain(); return rig.StemsReady.Count == 1; }, 3000));
        await rig.Settle();

        var analysis = rig.Run.Analysis;
        Assert.NotNull(analysis.Get<KeyAnalysis>());
        Assert.Equal(new StemPaths("/stems" + TrackA), analysis.Get<StemPaths>());
        Assert.Single(rig.DetectedBasic);
        Assert.Equal(123, rig.DetectedBasic[0].Bpm);
    }
}
