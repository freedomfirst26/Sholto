using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Harmony;
using Sholto.Data;
using Sholto.App.Analysis.Stems;
using Sholto.App.Audio;
using Sholto.App.Decks;
using Sholto.App.Library;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The exact <see cref="DeckChange"/> sequence a deck session raises for loads, analysis landing and
/// markers, so a restructuring of the session cannot reorder or drop a notification.</summary>
public class DeckSessionChangeOrderTests
{
    private static readonly Track ATrack = new("/music/a.mp3", "A", "Artist", TimeSpan.FromMinutes(3));

    private static BasicAnalysis Basic(double[]? downbeats = null)
    {
        var peaks = new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000);
        return new BasicAnalysis(peaks, 0, [], downbeats ?? []);
    }

    private static (DeckSessionRig Rig, ScriptedPorts Scripted, List<DeckChange> Changes) Build(bool swapping = false)
    {
        var scripted = new ScriptedPorts(new TestDeckFactory().Create());
        IDeckPorts ports = swapping ? new AnalysisSwappingPorts(scripted) : scripted;
        var rig = new DeckSessionRig(ports);
        var changes = new List<DeckChange>();
        rig.Session.Changed += changes.Add;
        return (rig, scripted, changes);
    }

    [Fact]
    public void BeginLoad_raises_load_state_then_track_then_markers_sections_and_reset()
    {
        var (rig, _, changes) = Build();

        rig.Session.BeginLoad(ATrack);

        Assert.Equal(
            [DeckChange.LoadState, DeckChange.LoadedTrack, DeckChange.BpmMultiplier, DeckChange.PlayPosition, DeckChange.Markers, DeckChange.Sections, DeckChange.AnalysisReset],
            changes);
    }

    [Fact]
    public void LoadStreaming_raises_the_full_load_sequence()
    {
        var (rig, _, changes) = Build();

        rig.Session.LoadStreaming(ATrack, "/music/a.mp3");

        Assert.Equal(
            [DeckChange.LoadedTrack, DeckChange.LoadState, DeckChange.BpmMultiplier, DeckChange.PlayPosition, DeckChange.IsLoaded, DeckChange.AnalysisReset],
            changes);
    }

    [Fact]
    public void LoadTrack_raises_the_full_load_sequence()
    {
        var (rig, _, changes) = Build();

        rig.Session.LoadTrack(ATrack, "/music/a.mp3", new float[8]);

        Assert.Equal(
            [DeckChange.LoadedTrack, DeckChange.LoadState, DeckChange.BpmMultiplier, DeckChange.PlayPosition, DeckChange.IsLoaded, DeckChange.AnalysisReset],
            changes);
    }

    [Fact]
    public void Unload_raises_the_full_load_sequence()
    {
        var (rig, _, changes) = Build();
        rig.Session.LoadStreaming(ATrack, "/music/a.mp3");
        changes.Clear();

        rig.Session.Unload();

        Assert.Equal(
            [DeckChange.LoadedTrack, DeckChange.LoadState, DeckChange.PlayPosition, DeckChange.IsLoaded, DeckChange.AnalysisReset],
            changes);
    }

    [Fact]
    public void Basic_analysis_set_after_construction_raises_sections_then_basic_ready()
    {
        var (rig, scripted, changes) = Build();

        scripted.Loading.Analysis.Set(Basic());

        Assert.Equal([DeckChange.Sections, DeckChange.BasicReady], changes);
    }

    [Fact]
    public void Basic_ready_without_basic_raises_only_basic_ready()
    {
        var (rig, scripted, changes) = Build();

        scripted.Loading.Analysis.Set<BasicAnalysis>(null!);

        Assert.Equal([DeckChange.BasicReady], changes);
    }

    [Fact]
    public void Key_stems_and_vocal_regions_each_raise_only_their_own_change()
    {
        var (rig, scripted, changes) = Build();

        scripted.Loading.Analysis.Set(new KeyAnalysis(new Key(PitchClass: 0, IsMajor: true)));
        Assert.Equal([DeckChange.KeyReady], changes);
        changes.Clear();

        scripted.Loading.Analysis.Set(new StemPaths("/cache/a"));
        Assert.Equal([DeckChange.StemsReady], changes);
        changes.Clear();

        scripted.Loading.Analysis.Set<IReadOnlyList<VocalRegion>>(new List<VocalRegion>());
        Assert.Equal([DeckChange.VocalRegionsReady], changes);
    }

    [Fact]
    public void A_basic_analysis_on_the_previous_tracks_analysis_raises_nothing_after_BeginLoad()
        => AssertPreviousAnalysisIsDetached(s => s.BeginLoad(ATrack));

    [Fact]
    public void A_basic_analysis_on_the_previous_tracks_analysis_raises_nothing_after_LoadStreaming()
        => AssertPreviousAnalysisIsDetached(s => s.LoadStreaming(ATrack, "/music/a.mp3"));

    [Fact]
    public void A_basic_analysis_on_the_previous_tracks_analysis_raises_nothing_after_LoadTrack()
        => AssertPreviousAnalysisIsDetached(s => s.LoadTrack(ATrack, "/music/a.mp3", new float[8]));

    private static void AssertPreviousAnalysisIsDetached(Action<IDeckSession> load)
    {
        var (rig, _, changes) = Build(swapping: true);
        var held = rig.Ports.Loading.Analysis;
        load(rig.Session);
        Assert.NotSame(held, rig.Ports.Loading.Analysis);
        changes.Clear();

        held.Set(Basic());

        Assert.Empty(changes);
    }

    [Fact]
    public void Unload_keeps_the_previous_analysis_subscription()
    {
        var (rig, _, changes) = Build(swapping: true);
        var held = rig.Ports.Loading.Analysis;
        rig.Session.Unload();
        Assert.NotSame(held, rig.Ports.Loading.Analysis);
        changes.Clear();

        held.Set(Basic());

        Assert.Equal([DeckChange.BasicReady], changes);
    }

    [Fact]
    public void SetMarkers_raises_markers_once_and_keeps_the_array()
    {
        var (rig, _, changes) = Build();
        double[] marks = [1.0, 2.0];

        rig.Session.SetMarkers(marks);

        Assert.Equal([DeckChange.Markers], changes);
        Assert.Same(marks, rig.Session.MarkerSecs);
    }

    [Fact]
    public void Sections_are_empty_again_after_BeginLoad()
    {
        var (rig, scripted, _) = Build();
        scripted.Loading.Analysis.Set(Basic([0.0, 2.0]));

        rig.Session.BeginLoad(ATrack);

        Assert.Empty(rig.Session.Sections);
    }

    private static double Nearest(double[]? downbeats, double positionSec, bool withAnalysis = true)
    {
        var (rig, scripted, _) = Build();
        if (withAnalysis) scripted.Loading.Analysis.Set(Basic(downbeats));
        scripted.ScriptedPlayhead.PositionFrames = (long)Math.Round(positionSec * 48000);
        return rig.Session.NearestDownbeatSec();
    }

    [Fact]
    public void Nearest_downbeat_without_analysis_is_minus_one() => Assert.Equal(-1, Nearest(null, 1, withAnalysis: false));

    [Fact]
    public void Nearest_downbeat_with_no_downbeats_is_minus_one() => Assert.Equal(-1, Nearest([], 1));

    [Fact]
    public void Nearest_downbeat_before_the_first_is_the_first() => Assert.Equal(2.0, Nearest([2.0, 4.0, 6.0], 0.5));

    [Fact]
    public void Nearest_downbeat_after_the_last_is_the_last() => Assert.Equal(6.0, Nearest([2.0, 4.0, 6.0], 9.0));

    [Fact]
    public void Nearest_downbeat_on_an_exact_hit_is_that_downbeat() => Assert.Equal(4.0, Nearest([2.0, 4.0, 6.0], 4.0));

    [Fact]
    public void Nearest_downbeat_between_two_is_the_nearer() => Assert.Equal(6.0, Nearest([2.0, 4.0, 6.0], 5.5));

    [Fact]
    public void Nearest_downbeat_equidistant_is_the_earlier() => Assert.Equal(4.0, Nearest([2.0, 4.0, 6.0], 5.0));
}
