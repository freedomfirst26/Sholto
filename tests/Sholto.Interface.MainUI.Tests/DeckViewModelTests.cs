using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The deck view model as a pure projection: events published on a bus come out as the bound
/// properties, only what moved is notified, events for the other deck are ignored, a view model built late
/// is replayed the state, and every action is exactly one command.</summary>
public class DeckViewModelTests
{
    private readonly DeckTrack SomeTrack = new("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3));

    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly FakeFrameClock _clock = new();
    private readonly List<string?> _changed = [];
    private readonly DeckViewModel _deck;

    public DeckViewModelTests()
    {
        AvaloniaTestApp.EnsureStarted();
        _deck = NewDeck(0);
        _deck.PropertyChanged += (_, e) => _changed.Add(e.PropertyName);
    }

    private DeckViewModel NewDeck(int index) =>
        new(index, _bus, _bus, new ThemeStackFactory().Build().Context, new NoPeaksFactory(),
            new DiscBloomFactory(_clock));

    private DeckAnalysis AnalysisWithBasic(double bpm = 128.0) => new(
        new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
        bpm, [], [], null, false, null);

    private static DeckAnalysis EmptyAnalysis() => new(null, 0, [], [], null, false, null);

    private DeckContentChanged Content(
        int deck, DeckLoadState state, bool isLoaded, DeckAnalysis? analysis = null, DeckTrack? track = null) =>
        new(deck, track, state, isLoaded, analysis);

    private F9RecordingCommandHandler<T> Record<T>() where T : struct, ICommand
    {
        var handler = new F9RecordingCommandHandler<T>();
        _bus.Register<T>(handler);
        return handler;
    }

    // ---- DeckFrame ------------------------------------------------------------------------------

    [Fact]
    public void A_frame_moves_the_position_speed_and_performance_flags()
    {
        _bus.Publish(new DeckFrame(0, 0.25, 12.0, 1.04, true, true, 7.5));

        Assert.Equal(0.25, _deck.PlayPosition);
        Assert.Equal(1.04, _deck.PlaybackSpeed);
        Assert.True(_deck.IsScrubbing);
        Assert.True(_deck.IsScratching);
        Assert.Equal(7.5, _deck.MagneticGlowSec);
        Assert.Contains(nameof(DeckViewModel.PlayPosition), _changed);
        Assert.Contains(nameof(DeckViewModel.DiscAngle), _changed);
        Assert.Contains(nameof(DeckViewModel.PlaybackSpeed), _changed);
        Assert.Contains(nameof(DeckViewModel.IsScrubbing), _changed);
        Assert.Contains(nameof(DeckViewModel.IsScratching), _changed);
        Assert.Contains(nameof(DeckViewModel.MagneticGlowSec), _changed);
    }

    [Fact]
    public void A_second_identical_frame_raises_nothing()
    {
        var frame = new DeckFrame(0, 0.25, 12.0, 1.04, true, true, 7.5);
        _bus.Publish(frame);
        _changed.Clear();

        _bus.Publish(frame);

        Assert.Empty(_changed);
    }

    [Fact]
    public void A_frame_notifies_only_what_moved()
    {
        _bus.Publish(new DeckFrame(0, 0.0, 0.0, 1.0, false, false, -1));
        Assert.Empty(_changed);   // identical to the starting picture

        _bus.Publish(new DeckFrame(0, 0.0, 0.0, 1.0, true, false, -1));

        Assert.Equal([nameof(DeckViewModel.IsScrubbing)], _changed);
    }

    [Fact]
    public void A_frame_for_the_other_deck_is_ignored()
    {
        _bus.Publish(new DeckFrame(1, 0.5, 30.0, 1.1, true, true, 3.0));

        Assert.Empty(_changed);
        Assert.Equal(0.0, _deck.PlayPosition);
        Assert.False(_deck.IsScrubbing);
        Assert.Equal(-1, _deck.MagneticGlowSec);
    }

    [Fact]
    public void The_disc_turns_once_per_bar_of_the_loaded_tracks_tempo_and_is_still_without_analysis()
    {
        Assert.Equal(0.0, _deck.DiscAngle);

        _bus.Publish(Content(0, DeckLoadState.Loaded, true, AnalysisWithBasic(bpm: 128.0)));
        _bus.Publish(new DeckFrame(0, 0.1, 3.75, 1.0, false, false, -1));   // 128 bpm: one bar is 1.875 s

        Assert.Equal(720.0, _deck.DiscAngle, 6);
    }

    // ---- Play state -----------------------------------------------------------------------------

    [Fact]
    public void The_play_phase_drives_IsPlaying_and_PlayState()
    {
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Playing, false));

        Assert.True(_deck.IsPlaying);
        Assert.Equal(PlayPhase.Playing, _deck.PlayState);
        Assert.Equal(1.0, _deck.DiscRingOpacity);
        Assert.Contains(nameof(DeckViewModel.IsPlaying), _changed);
        Assert.Contains(nameof(DeckViewModel.PlayState), _changed);

        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Stopped, false));

        Assert.False(_deck.IsPlaying);
        Assert.Equal(PlayPhase.Stopped, _deck.PlayState);
    }

    [Fact]
    public void While_ending_the_ring_opacity_follows_the_flash_phase()
    {
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Ending, true));
        Assert.Equal(1.0, _deck.DiscRingOpacity);
        Assert.True(_deck.IsPlaying);

        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Ending, false));
        Assert.Equal(0.35, _deck.DiscRingOpacity);
    }

    [Fact]
    public void A_flash_phase_while_not_ending_leaves_the_ring_solid()
    {
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Playing, false));

        Assert.Equal(1.0, _deck.DiscRingOpacity);
    }

    [Fact]
    public void A_play_state_for_the_other_deck_is_ignored()
    {
        _bus.Publish(new DeckPlayStateChanged(1, PlayPhase.Playing, false));

        Assert.False(_deck.IsPlaying);
        Assert.Empty(_changed);
    }

    // ---- Content --------------------------------------------------------------------------------

    [Fact]
    public void A_loaded_track_with_analysis_can_play_and_shows_the_info_at_full_opacity()
    {
        var analysis = AnalysisWithBasic();

        _bus.Publish(Content(0, DeckLoadState.Loaded, true, analysis, SomeTrack));

        Assert.Same(SomeTrack, _deck.LoadedTrack);
        Assert.Equal(DeckLoadState.Loaded, _deck.LoadState);
        Assert.Equal(1.0, _deck.InfoOpacity);
        Assert.True(_deck.IsLoaded);
        Assert.True(_deck.HasAnalysis);
        Assert.True(_deck.CanPlay);
        Assert.Same(analysis, _deck.Analysis);
        Assert.Same(analysis.Peaks, _deck.Peaks);
        Assert.Contains(nameof(DeckViewModel.LoadedTrack), _changed);
        Assert.Contains(nameof(DeckViewModel.LoadState), _changed);
        Assert.Contains(nameof(DeckViewModel.InfoOpacity), _changed);
        Assert.Contains(nameof(DeckViewModel.IsLoaded), _changed);
        Assert.Contains(nameof(DeckViewModel.CanPlay), _changed);
    }

    [Fact]
    public void A_landed_key_shows_its_Camelot_code_and_notifies_it()
    {
        _bus.Publish(Content(0, DeckLoadState.Loaded, true, AnalysisWithBasic() with { Key = new KeyRef(0, true) }, SomeTrack));

        Assert.Equal("8B", _deck.Camelot);
        Assert.True(_deck.HasKey);
        Assert.Contains(nameof(DeckViewModel.Camelot), _changed);
    }

    [Fact]
    public void Landed_stems_show_the_stem_chips()
    {
        Assert.False(_deck.HasStems);

        _bus.Publish(Content(0, DeckLoadState.Loaded, true, AnalysisWithBasic() with { HasStems = true }, SomeTrack));

        Assert.True(_deck.HasStems);
        Assert.Contains(nameof(DeckViewModel.HasStems), _changed);
    }

    [Fact]
    public void Landed_vocal_regions_reach_VocalRegions()
    {
        Assert.Null(_deck.VocalRegions);
        VocalRegion[] regions = [new VocalRegion(1, 2)];

        _bus.Publish(Content(0, DeckLoadState.Loaded, true, AnalysisWithBasic() with { VocalRegions = regions }, SomeTrack));

        Assert.Same(regions, _deck.VocalRegions);
        Assert.Contains(nameof(DeckViewModel.VocalRegions), _changed);
    }

    [Fact]
    public void A_loaded_track_whose_analysis_has_not_landed_cannot_play()
    {
        _bus.Publish(Content(0, DeckLoadState.Loaded, true, EmptyAnalysis(), SomeTrack));

        Assert.True(_deck.IsLoaded);
        Assert.False(_deck.HasAnalysis);
        Assert.False(_deck.CanPlay);
    }

    [Fact]
    public void While_loading_the_info_is_dimmed_and_the_deck_cannot_play()
    {
        _bus.Publish(Content(0, DeckLoadState.Loading, true, AnalysisWithBasic(), SomeTrack));

        Assert.Equal(DeckLoadState.Loading, _deck.LoadState);
        Assert.Equal(0.3, _deck.InfoOpacity);
        Assert.False(_deck.CanPlay);
    }

    [Fact]
    public void An_idle_deck_with_no_track_shows_nothing_loaded()
    {
        _bus.Publish(Content(0, DeckLoadState.Loaded, true, AnalysisWithBasic(), SomeTrack));

        _bus.Publish(Content(0, DeckLoadState.Idle, false));

        Assert.Null(_deck.LoadedTrack);
        Assert.False(_deck.IsLoaded);
        Assert.False(_deck.HasAnalysis);
        Assert.False(_deck.CanPlay);
        Assert.Equal(0.3, _deck.InfoOpacity);
    }

    [Fact]
    public void Content_for_the_other_deck_is_ignored()
    {
        _bus.Publish(Content(1, DeckLoadState.Loaded, true, AnalysisWithBasic(), SomeTrack));

        Assert.Null(_deck.LoadedTrack);
        Assert.Empty(_changed);
    }

    // ---- Tempo ----------------------------------------------------------------------------------

    [Fact]
    public void Tempo_shows_the_effective_and_original_bpm_and_the_range()
    {
        _bus.Publish(new DeckTempoChanged(0, 128.0, 0.5, 64.0, 1.0, 0.06, true, false));

        Assert.Equal(128.0, _deck.SourceBpm);
        Assert.Equal(0.5, _deck.BpmMultiplier);
        Assert.Equal(64.0, _deck.EffectiveBpm);
        Assert.True(_deck.HasBpm);
        Assert.Equal("64.0 BPM", _deck.BpmDisplay);
        Assert.Equal("64.0", _deck.BpmDisplayShort);
        Assert.Equal("64.0", _deck.OriginalBpmShort);
        Assert.True(_deck.ShowOriginalBpm);
        Assert.Equal("±6%", _deck.TempoRangeDisplay);
    }

    [Fact]
    public void The_widest_tempo_range_reads_WIDE()
    {
        _bus.Publish(new DeckTempoChanged(0, 128.0, 1.0, 128.0, 1.0, 1.0, false, false));

        Assert.Equal("WIDE", _deck.TempoRangeDisplay);
    }

    [Fact]
    public void The_original_bpm_chip_shows_for_a_shifted_fader_or_a_magnet_adjustment_only()
    {
        _bus.Publish(new DeckTempoChanged(0, 128.0, 1.0, 128.0, 1.0, 0.06, false, false));
        Assert.False(_deck.ShowOriginalBpm);

        _bus.Publish(new DeckTempoChanged(0, 128.0, 1.0, 128.1, 1.0, 0.06, false, true));
        Assert.True(_deck.ShowOriginalBpm);
        Assert.True(_deck.WasMagnetAdjusted);
    }

    [Fact]
    public void Without_a_source_bpm_the_displays_are_empty()
    {
        _bus.Publish(new DeckTempoChanged(0, 0.0, 1.0, 0.0, 1.0, 0.06, false, false));

        Assert.False(_deck.HasBpm);
        Assert.Equal("", _deck.BpmDisplay);
        Assert.Equal("", _deck.BpmDisplayShort);
        Assert.Equal("", _deck.OriginalBpmShort);
    }

    [Fact]
    public void Tempo_for_the_other_deck_is_ignored()
    {
        _bus.Publish(new DeckTempoChanged(1, 128.0, 1.0, 128.0, 1.0, 0.06, false, false));

        Assert.Equal(0.0, _deck.SourceBpm);
        Assert.Empty(_changed);
    }

    // ---- Loop, edit, markers, mix ---------------------------------------------------------------

    [Fact]
    public void An_active_loop_shows_its_bounds_and_an_inactive_one_has_none()
    {
        _bus.Publish(new DeckLoopChanged(0, true, 4.0, 8.0));

        Assert.True(_deck.IsLooping);
        Assert.Equal(4.0, _deck.LoopStartSec);
        Assert.Equal(8.0, _deck.LoopEndSec);

        _bus.Publish(new DeckLoopChanged(0, false, 0, 0));

        Assert.False(_deck.IsLooping);
        Assert.Null(_deck.LoopStartSec);
        Assert.Null(_deck.LoopEndSec);
    }

    [Fact]
    public void The_edit_state_drives_the_tune_editor_and_grid_flags()
    {
        _bus.Publish(new DeckEditChanged(0, true, true, true));

        Assert.True(_deck.EditOpen);
        Assert.True(_deck.GridTuneActive);
        Assert.True(_deck.GridEditActive);
        Assert.True(_deck.IsGridNudged);

        _bus.Publish(new DeckEditChanged(0, false, false, true));

        Assert.False(_deck.EditOpen);
        Assert.False(_deck.GridTuneActive);
        Assert.False(_deck.GridEditActive);
        Assert.True(_deck.IsGridNudged);
    }

    [Fact]
    public void Markers_are_the_seconds_the_App_published()
    {
        var markers = new[] { 1.5, 30.0 };

        _bus.Publish(new DeckMarkersChanged(0, markers));

        Assert.Same(markers, _deck.MarkerSecs);
        Assert.Contains(nameof(DeckViewModel.MarkerSecs), _changed);
    }

    [Fact]
    public void A_measured_fader_shows_its_gain_and_whether_it_is_muted()
    {
        _bus.Publish(new DeckMixChanged(0, true, 0.8, 0.4, false));

        Assert.True(_deck.GainKnown);
        Assert.Equal(0.8, _deck.ChannelGain);
        Assert.Equal(0.4, _deck.EffectiveGain);
        Assert.False(_deck.IsMuted);

        _bus.Publish(new DeckMixChanged(0, true, 0.0, 0.0, true));

        Assert.True(_deck.IsMuted);
    }

    [Fact]
    public void An_unmeasured_fader_has_no_channel_gain()
    {
        _bus.Publish(new DeckMixChanged(0, false, 0.0, 0.0, false));

        Assert.False(_deck.GainKnown);
        Assert.Null(_deck.ChannelGain);
        Assert.False(_deck.IsMuted);
    }

    // ---- Stems, echo, cue -----------------------------------------------------------------------

    [Fact]
    public void Every_stem_starts_active()
    {
        Assert.True(_deck.DrumsActive);
        Assert.True(_deck.VocalsActive);
        Assert.True(_deck.InstrumentalActive);
    }

    [Fact]
    public void A_muted_stem_is_not_active_and_unmuting_makes_it_active_again()
    {
        _bus.Publish(new StemMuteChanged(0, 0, true));
        _bus.Publish(new StemMuteChanged(0, 1, true));
        _bus.Publish(new StemMuteChanged(0, 2, true));

        Assert.False(_deck.DrumsActive);
        Assert.False(_deck.VocalsActive);
        Assert.False(_deck.InstrumentalActive);

        _bus.Publish(new StemMuteChanged(0, 1, false));

        Assert.False(_deck.DrumsActive);
        Assert.True(_deck.VocalsActive);
        Assert.False(_deck.InstrumentalActive);
    }

    [Fact]
    public void A_stem_mute_for_the_other_deck_is_ignored()
    {
        _bus.Publish(new StemMuteChanged(1, 0, true));

        Assert.True(_deck.DrumsActive);
        Assert.Empty(_changed);
    }

    [Fact]
    public void Echo_and_headphone_cue_follow_their_events()
    {
        _bus.Publish(new EchoChanged(0, true));
        _bus.Publish(new HeadphoneCueChanged(0, true));

        Assert.True(_deck.EchoActive);
        Assert.True(_deck.CueActive);

        _bus.Publish(new EchoChanged(0, false));
        _bus.Publish(new HeadphoneCueChanged(0, false));

        Assert.False(_deck.EchoActive);
        Assert.False(_deck.CueActive);
    }

    [Fact]
    public void Echo_and_cue_for_the_other_deck_are_ignored()
    {
        _bus.Publish(new EchoChanged(1, true));
        _bus.Publish(new HeadphoneCueChanged(1, true));

        Assert.False(_deck.EchoActive);
        Assert.False(_deck.CueActive);
    }

    // ---- Replay ---------------------------------------------------------------------------------

    [Fact]
    public void A_view_model_built_after_the_events_shows_them()
    {
        _bus.Publish(Content(1, DeckLoadState.Loaded, true, AnalysisWithBasic(), SomeTrack));
        _bus.Publish(new DeckTempoChanged(1, 128.0, 1.0, 128.0, 1.0, 0.06, false, false));
        _bus.Publish(new DeckLoopChanged(1, true, 4.0, 8.0));
        _bus.Publish(new DeckEditChanged(1, true, false, false));
        _bus.Publish(new DeckMarkersChanged(1, [2.0]));
        _bus.Publish(new DeckMixChanged(1, true, 0.7, 0.7, false));
        _bus.Publish(new DeckPlayStateChanged(1, PlayPhase.Playing, false));
        _bus.Publish(new StemMuteChanged(1, 2, true));
        _bus.Publish(new EchoChanged(1, true));
        _bus.Publish(new HeadphoneCueChanged(1, true));
        _bus.Publish(new DeckFrame(1, 0.4, 9.0, 1.02, true, false, 5.0));

        var late = NewDeck(1);

        Assert.Same(SomeTrack, late.LoadedTrack);
        Assert.True(late.CanPlay);
        Assert.Equal("128.0 BPM", late.BpmDisplay);
        Assert.True(late.IsLooping);
        Assert.True(late.EditOpen);
        Assert.Equal(new[] { 2.0 }, late.MarkerSecs);
        Assert.Equal(0.7, late.ChannelGain);
        Assert.True(late.IsPlaying);
        Assert.False(late.InstrumentalActive);
        Assert.True(late.EchoActive);
        Assert.True(late.CueActive);
        Assert.Equal(0.4, late.PlayPosition);
        Assert.Equal(1.02, late.PlaybackSpeed);
        Assert.True(late.IsScrubbing);
        Assert.Equal(5.0, late.MagneticGlowSec);
    }

    // ---- Actions: one command each --------------------------------------------------------------

    [Fact]
    public void ToggleEditor_sends_ToggleTuneEditor_for_its_deck()
    {
        var sent = Record<ToggleTuneEditor>();

        NewDeck(1).ToggleEditor();

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void CloseEditor_sends_CloseTuneEditor_for_its_deck()
    {
        var sent = Record<CloseTuneEditor>();

        NewDeck(1).CloseEditor();

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Theory]
    [InlineData(BpmMultiplierOp.Toggle)]
    [InlineData(BpmMultiplierOp.Halve)]
    [InlineData(BpmMultiplierOp.Double)]
    [InlineData(BpmMultiplierOp.Reset)]
    public void ChangeBpm_sends_ChangeBpmMultiplier_with_the_op(BpmMultiplierOp op)
    {
        var sent = Record<ChangeBpmMultiplier>();

        NewDeck(0).ChangeBpm(op);

        var command = Assert.Single(sent.Received);
        Assert.Equal(0, command.Deck);
        Assert.Equal(op, command.Op);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void AdjustBpm_sends_AdjustBpm_with_the_delta()
    {
        var sent = Record<AdjustBpm>();

        NewDeck(1).AdjustBpm(-0.1);

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(-0.1, command.Delta);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void NudgeGridFine_sends_NudgeGridFine_with_the_seconds()
    {
        var sent = Record<NudgeGridFine>();

        NewDeck(1).NudgeGridFine(0.01);

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(0.01, command.Seconds);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void NudgeGrid_sends_NudgeGrid_with_the_beats()
    {
        var sent = Record<NudgeGrid>();

        NewDeck(0).NudgeGrid(-1);

        var command = Assert.Single(sent.Received);
        Assert.Equal(0, command.Deck);
        Assert.Equal(-1, command.Beats);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void ResetToAnalysis_sends_ResetDeckToAnalysis_for_its_deck()
    {
        var sent = Record<ResetDeckToAnalysis>();

        NewDeck(1).ResetToAnalysis();

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void ClickGrid_sends_ClickGrid_with_the_seconds()
    {
        var sent = Record<ClickGrid>();

        NewDeck(0).ClickGrid(12.5);

        var command = Assert.Single(sent.Received);
        Assert.Equal(0, command.Deck);
        Assert.Equal(12.5, command.Seconds);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void An_action_changes_nothing_on_the_view_model_until_the_App_answers()
    {
        Record<ToggleTuneEditor>();

        _deck.ToggleEditor();

        Assert.False(_deck.EditOpen);
        Assert.Empty(_changed);
    }

    // ---- Allocation -----------------------------------------------------------------------------

    [Fact]
    public void A_playing_frame_allocates_only_when_the_ring_colour_steps()
    {
        var notifications = 0;
        _deck.PropertyChanged += (_, _) => notifications++;
        // The fixture's own _changed list records every notification; size it up front so its doubling
        // growth is not counted as the view model's allocation.
        _changed.Capacity = 1 << 20;

        // One 5-minute track at 60 fps is 18,000 frames; the position advances 1/18000 per frame.
        const int totalFrames = 18_000;
        const int warmUpFrames = 1_000;
        // Measured as one AvaloniaPropertyChangedEventArgs<Color> per SolidColorBrush.Color set.
        const long bytesPerColourStep = 64;

        DeckFrame Frame(int i) =>
            new(0, i / (double)totalFrames, i * 0.01, 1.0 + (i % 7) * 0.001, i % 2 == 0, i % 3 == 0, i % 5);

        var brush = (Avalonia.Media.SolidColorBrush)_deck.DiscRingBrush;

        // A full unmeasured pass so tiered JIT/OSR has finished before measuring.
        for (var i = 0; i < totalFrames; i++) _bus.Publish(Frame(i));

        // Best of 3: a sporadic one-off runtime allocation on a random frame must not fail the budget.
        const int attempts = 3;
        var measuredFrames = totalFrames - warmUpFrames;
        var results = new List<(long Allocated, int Steps)>();
        var passed = false;
        for (var attempt = 0; attempt < attempts && !passed; attempt++)
        {
            var colourChanges = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = warmUpFrames; i < totalFrames; i++)
            {
                var colourBefore = brush.Color;
                _bus.Publish(Frame(i));
                if (brush.Color != colourBefore) colourChanges++;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            results.Add((allocated, colourChanges));
            passed = allocated <= colourChanges * bytesPerColourStep;
        }

        Assert.True(notifications > 0);
        Assert.True(results[^1].Steps < measuredFrames * 0.05,
            $"colour changed on {results[^1].Steps} of {measuredFrames} frames");
        Assert.True(passed,
            "allocated over budget in every attempt: "
            + string.Join("; ", results.Select(r => $"{r.Allocated} bytes over {r.Steps} colour changes")));
    }

    [Fact]
    public void Sections_event_is_projected_and_ignored_for_the_other_deck()
    {
        var sections = new List<DeckSection> { new(DeckSectionKind.Intro, 0, 8), new(DeckSectionKind.Drop, 8, 16) };

        _bus.Publish(new DeckSectionsChanged(1, [], new DeckPhraseGrid(0, 8), 0, 0, 0));
        Assert.Empty(_deck.Sections);

        _bus.Publish(new DeckSectionsChanged(0, sections, new DeckPhraseGrid(4, 8), 0.25, 1.875, 24));

        Assert.Equal(sections, _deck.Sections);
        Assert.Equal(4, _deck.PhraseGrid.PhaseBar);
        Assert.Equal(0.25, _deck.FirstDownbeatSec);
        Assert.Equal(1.875, _deck.BarPeriodSec);
        Assert.Equal(24, _deck.TotalBars);
        Assert.Contains(nameof(DeckViewModel.Sections), _changed);
        Assert.True(_deck.HasSections);
        Assert.Contains(nameof(DeckViewModel.SectionMapVisible), _changed);
    }

    [Fact]
    public void Sections_without_a_bar_grid_do_not_count_as_sections()
    {
        _bus.Publish(new DeckSectionsChanged(0, [new(DeckSectionKind.Intro, 0, 8)], new DeckPhraseGrid(0, 8), 0, 0, 0));
        Assert.False(_deck.HasSections);
    }
}
