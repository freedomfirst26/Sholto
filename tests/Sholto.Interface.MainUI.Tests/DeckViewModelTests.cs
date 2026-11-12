using Microsoft.Extensions.Options;
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
[Collection(DeckViewModelCollection.Name)]
public class DeckViewModelTests
{
    private readonly DeckViewModelRig rig = new();

    // ---- DeckFrame ------------------------------------------------------------------------------

    [Fact]
    public void A_frame_moves_the_position_speed_and_performance_flags()
    {
        rig.Bus.Publish(new DeckFrame(0, 0.25, 12.0, 1.04, true, true, 7.5));

        Assert.Equal(0.25, rig.Deck.PlayPosition);
        Assert.Equal(1.04, rig.Deck.PlaybackSpeed);
        Assert.True(rig.Deck.IsScrubbing);
        Assert.True(rig.Deck.IsScratching);
        Assert.Equal(7.5, rig.Deck.MagneticGlowSec);
        Assert.Contains(nameof(DeckViewModel.PlayPosition), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.DiscAngle), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.PlaybackSpeed), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.IsScrubbing), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.IsScratching), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.MagneticGlowSec), rig.Changed);
    }

    [Fact]
    public void A_second_identical_frame_raises_nothing()
    {
        var frame = new DeckFrame(0, 0.25, 12.0, 1.04, true, true, 7.5);
        rig.Bus.Publish(frame);
        rig.Changed.Clear();

        rig.Bus.Publish(frame);

        Assert.Empty(rig.Changed);
    }

    [Fact]
    public void A_frame_notifies_only_what_moved()
    {
        rig.Bus.Publish(new DeckFrame(0, 0.0, 0.0, 1.0, false, false, -1));
        Assert.Empty(rig.Changed);   // identical to the starting picture

        rig.Bus.Publish(new DeckFrame(0, 0.0, 0.0, 1.0, true, false, -1));

        Assert.Equal([nameof(DeckViewModel.IsScrubbing)], rig.Changed);
    }

    [Fact]
    public void A_frame_for_the_other_deck_is_ignored()
    {
        rig.Bus.Publish(new DeckFrame(1, 0.5, 30.0, 1.1, true, true, 3.0));

        Assert.Empty(rig.Changed);
        Assert.Equal(0.0, rig.Deck.PlayPosition);
        Assert.False(rig.Deck.IsScrubbing);
        Assert.Equal(-1, rig.Deck.MagneticGlowSec);
    }

    [Fact]
    public void The_disc_turns_once_per_bar_of_the_loaded_tracks_tempo_and_is_still_without_analysis()
    {
        Assert.Equal(0.0, rig.Deck.DiscAngle);

        rig.Bus.Publish(rig.Content(0, DeckLoadState.Loaded, true, rig.AnalysisWithBasic(bpm: 128.0)));
        rig.Bus.Publish(new DeckFrame(0, 0.1, 3.75, 1.0, false, false, -1));   // 128 bpm: one bar is 1.875 s

        Assert.Equal(720.0, rig.Deck.DiscAngle, 6);
    }

    // ---- Play state -----------------------------------------------------------------------------

    [Fact]
    public void The_play_phase_drives_IsPlaying_and_PlayState()
    {
        rig.Bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Playing, false));

        Assert.True(rig.Deck.IsPlaying);
        Assert.Equal(PlayPhase.Playing, rig.Deck.PlayState);
        Assert.Equal(1.0, rig.Deck.DiscRingOpacity);
        Assert.Contains(nameof(DeckViewModel.IsPlaying), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.PlayState), rig.Changed);

        rig.Bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Stopped, false));

        Assert.False(rig.Deck.IsPlaying);
        Assert.Equal(PlayPhase.Stopped, rig.Deck.PlayState);
    }

    [Fact]
    public void While_ending_the_ring_opacity_follows_the_flash_phase()
    {
        rig.Bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Ending, true));
        Assert.Equal(1.0, rig.Deck.DiscRingOpacity);
        Assert.True(rig.Deck.IsPlaying);

        rig.Bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Ending, false));
        Assert.Equal(0.35, rig.Deck.DiscRingOpacity);
    }

    [Fact]
    public void A_flash_phase_while_not_ending_leaves_the_ring_solid()
    {
        rig.Bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Playing, false));

        Assert.Equal(1.0, rig.Deck.DiscRingOpacity);
    }

    [Fact]
    public void A_play_state_for_the_other_deck_is_ignored()
    {
        rig.Bus.Publish(new DeckPlayStateChanged(1, PlayPhase.Playing, false));

        Assert.False(rig.Deck.IsPlaying);
        Assert.Empty(rig.Changed);
    }

    [Fact]
    public void While_loading_the_info_is_dimmed_and_the_deck_cannot_play()
    {
        rig.Bus.Publish(rig.Content(0, DeckLoadState.Loading, true, rig.AnalysisWithBasic(), rig.SomeTrack));

        Assert.Equal(DeckLoadState.Loading, rig.Deck.LoadState);
        Assert.Equal(0.3, rig.Deck.InfoOpacity);
        Assert.False(rig.Deck.CanPlay);
    }

    [Fact]
    public void Content_for_the_other_deck_is_ignored()
    {
        rig.Bus.Publish(rig.Content(1, DeckLoadState.Loaded, true, rig.AnalysisWithBasic(), rig.SomeTrack));

        Assert.Null(rig.Deck.LoadedTrack);
        Assert.Empty(rig.Changed);
    }

    // ---- Loop, edit, markers, mix ---------------------------------------------------------------

    [Fact]
    public void An_active_loop_shows_its_bounds_and_an_inactive_one_has_none()
    {
        rig.Bus.Publish(new DeckLoopChanged(0, true, 4.0, 8.0));

        Assert.True(rig.Deck.IsLooping);
        Assert.Equal(4.0, rig.Deck.LoopStartSec);
        Assert.Equal(8.0, rig.Deck.LoopEndSec);

        rig.Bus.Publish(new DeckLoopChanged(0, false, 0, 0));

        Assert.False(rig.Deck.IsLooping);
        Assert.Null(rig.Deck.LoopStartSec);
        Assert.Null(rig.Deck.LoopEndSec);
    }

    [Fact]
    public void The_edit_state_drives_the_tune_editor_and_grid_flags()
    {
        rig.Bus.Publish(new DeckEditChanged(0, true, true, true));

        Assert.True(rig.Deck.EditOpen);
        Assert.True(rig.Deck.GridTuneActive);
        Assert.True(rig.Deck.GridEditActive);
        Assert.True(rig.Deck.IsGridNudged);

        rig.Bus.Publish(new DeckEditChanged(0, false, false, true));

        Assert.False(rig.Deck.EditOpen);
        Assert.False(rig.Deck.GridTuneActive);
        Assert.False(rig.Deck.GridEditActive);
        Assert.True(rig.Deck.IsGridNudged);
    }

    [Fact]
    public void Markers_are_the_seconds_the_App_published()
    {
        var markers = new[] { 1.5, 30.0 };

        rig.Bus.Publish(new DeckMarkersChanged(0, markers));

        Assert.Same(markers, rig.Deck.MarkerSecs);
        Assert.Contains(nameof(DeckViewModel.MarkerSecs), rig.Changed);
    }

    [Fact]
    public void A_measured_fader_shows_its_gain_and_whether_it_is_muted()
    {
        rig.Bus.Publish(new DeckMixChanged(0, true, 0.8, 0.4, false));

        Assert.True(rig.Deck.GainKnown);
        Assert.Equal(0.8, rig.Deck.ChannelGain);
        Assert.Equal(0.4, rig.Deck.EffectiveGain);
        Assert.False(rig.Deck.IsMuted);

        rig.Bus.Publish(new DeckMixChanged(0, true, 0.0, 0.0, true));

        Assert.True(rig.Deck.IsMuted);
    }

    [Fact]
    public void An_unmeasured_fader_has_no_channel_gain()
    {
        rig.Bus.Publish(new DeckMixChanged(0, false, 0.0, 0.0, false));

        Assert.False(rig.Deck.GainKnown);
        Assert.Null(rig.Deck.ChannelGain);
        Assert.False(rig.Deck.IsMuted);
    }

    [Fact]
    public void Echo_and_headphone_cue_follow_their_events()
    {
        rig.Bus.Publish(new EchoChanged(0, true));
        rig.Bus.Publish(new HeadphoneCueChanged(0, true));

        Assert.True(rig.Deck.EchoActive);
        Assert.True(rig.Deck.CueActive);

        rig.Bus.Publish(new EchoChanged(0, false));
        rig.Bus.Publish(new HeadphoneCueChanged(0, false));

        Assert.False(rig.Deck.EchoActive);
        Assert.False(rig.Deck.CueActive);
    }

    [Fact]
    public void Echo_and_cue_for_the_other_deck_are_ignored()
    {
        rig.Bus.Publish(new EchoChanged(1, true));
        rig.Bus.Publish(new HeadphoneCueChanged(1, true));

        Assert.False(rig.Deck.EchoActive);
        Assert.False(rig.Deck.CueActive);
    }

    // ---- Replay ---------------------------------------------------------------------------------

    [Fact]
    public void A_view_model_built_after_the_events_shows_them()
    {
        rig.Bus.Publish(rig.Content(1, DeckLoadState.Loaded, true, rig.AnalysisWithBasic(), rig.SomeTrack));
        rig.Bus.Publish(new DeckTempoChanged(1, 128.0, 1.0, 128.0, 1.0, 0.06, false, false));
        rig.Bus.Publish(new DeckLoopChanged(1, true, 4.0, 8.0));
        rig.Bus.Publish(new DeckEditChanged(1, true, false, false));
        rig.Bus.Publish(new DeckMarkersChanged(1, [2.0]));
        rig.Bus.Publish(new DeckMixChanged(1, true, 0.7, 0.7, false));
        rig.Bus.Publish(new DeckPlayStateChanged(1, PlayPhase.Playing, false));
        rig.Bus.Publish(new StemMuteChanged(1, 2, true));
        rig.Bus.Publish(new EchoChanged(1, true));
        rig.Bus.Publish(new HeadphoneCueChanged(1, true));
        rig.Bus.Publish(new DeckFrame(1, 0.4, 9.0, 1.02, true, false, 5.0));

        var late = rig.NewDeck(1);

        Assert.Same(rig.SomeTrack, late.LoadedTrack);
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
}
