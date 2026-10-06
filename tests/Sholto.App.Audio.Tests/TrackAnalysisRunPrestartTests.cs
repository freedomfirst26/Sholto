using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio.Tests;

public class TrackAnalysisRunPrestartTests
{
    private const string TrackA = "/music/a.mp3";
    private const string TrackB = "/music/b.mp3";

    [Fact]
    public async Task Begin_load_starts_the_basic_lookup_and_the_stems_before_the_track_is_decoded()
    {
        var rig = new TrackAnalysisRunRig(stemsOverlap: true);
        rig.BeginLoad(TrackA);
        Assert.True(SpinWait.SpinUntil(
            () => rig.Pipeline.BeginRequested(TrackA) && rig.Pipeline.StemsRequested(TrackA), 2000));

        rig.Load(TrackA);
        rig.Pipeline.CompleteAll(TrackA, bpm: 123);
        Assert.True(SpinWait.SpinUntil(() => { rig.Drain(); return rig.StemsReady.Count == 1; }, 3000));
        await rig.Settle();

        Assert.Equal(1, rig.Pipeline.BeginCount(TrackA));
        Assert.Equal(1, rig.Pipeline.StemsCount(TrackA));
        Assert.Single(rig.DetectedBasic);
    }

    [Fact]
    public async Task On_cpu_begin_load_starts_no_stems_until_basic_completes()
    {
        var rig = new TrackAnalysisRunRig(stemsOverlap: false);
        rig.BeginLoad(TrackA);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.BeginRequested(TrackA), 2000));
        await Task.Delay(200);
        Assert.False(rig.Pipeline.StemsRequested(TrackA));

        rig.Load(TrackA);
        rig.Pipeline.CompleteBasicAndKey(TrackA, bpm: 123);

        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.StemsRequested(TrackA), 2000));
        await rig.Settle();
    }

    [Fact]
    public async Task Results_of_a_prestart_are_not_applied_before_the_track_is_loaded()
    {
        var rig = new TrackAnalysisRunRig(stemsOverlap: true);
        rig.BeginLoad(TrackA);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.StemsRequested(TrackA), 2000));

        rig.Pipeline.CompleteAll(TrackA, bpm: 123);
        await rig.Settle();

        Assert.Empty(rig.StemsReady);
        Assert.Empty(rig.DetectedBasic);

        rig.Load(TrackA);
        Assert.True(SpinWait.SpinUntil(() => { rig.Drain(); return rig.StemsReady.Count == 1; }, 3000));
        await rig.Settle();
    }

    [Fact]
    public async Task A_second_begin_load_cancels_the_first_prestart_and_drops_its_results()
    {
        var rig = new TrackAnalysisRunRig(stemsOverlap: true);
        rig.BeginLoad(TrackA);
        Assert.True(SpinWait.SpinUntil(
            () => rig.Pipeline.BeginRequested(TrackA) && rig.Pipeline.StemsRequested(TrackA), 2000));

        rig.BeginLoad(TrackB);

        Assert.True(rig.Pipeline.BeginToken(TrackA).IsCancellationRequested);
        Assert.True(rig.Pipeline.StemToken(TrackA).IsCancellationRequested);
        rig.Pipeline.CompleteAll(TrackA, bpm: 111);
        rig.Load(TrackB);
        rig.Pipeline.CompleteAll(TrackB, bpm: 123);
        Assert.True(SpinWait.SpinUntil(() => { rig.Drain(); return rig.StemsReady.Count == 1; }, 3000));
        await rig.Settle();

        Assert.Equal(123, Assert.Single(rig.DetectedBasic).Bpm);
        Assert.Equal(new StemPaths("/stems" + TrackB), rig.Run.Analysis.Get<StemPaths>());
    }

    [Fact]
    public async Task A_load_of_another_track_does_not_adopt_the_prestart()
    {
        var rig = new TrackAnalysisRunRig(stemsOverlap: true);
        rig.BeginLoad(TrackA);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.BeginRequested(TrackA), 2000));

        rig.Load(TrackB);

        Assert.True(rig.Pipeline.BeginToken(TrackA).IsCancellationRequested);
        Assert.True(SpinWait.SpinUntil(() => rig.Pipeline.BeginRequested(TrackB), 2000));
        await rig.Settle();
    }
}
