using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using DeckContent = Sholto.Data.DeckContentChanged<
    Sholto.App.Library.Track, Sholto.App.Audio.TrackAnalysis, Sholto.App.Analysis.Analyzers.Segments.SongSegment>;

namespace Sholto.App.Tests;

/// <summary>What a deck session announces on the bus through its event publisher: the whole starting picture
/// from the first moment (replayed to a late subscriber), then an event for each change an interface
/// shows, with the per-frame event allocating nothing.</summary>
public class DeckEventPublisherTests
{
    private readonly Track _track = new("/music/a.mp3", "A", "Artist", TimeSpan.FromMinutes(3));
    private readonly ScriptedPorts _ports = new(new TestDeckFactory().Create());
    private readonly DeckSessionRig _rig;

    public DeckEventPublisherTests()
    {
        _rig = new DeckSessionRig(_ports);
    }

    private RecordingHandler<T> Watch<T>() where T : struct, IEvent
    {
        var handler = new RecordingHandler<T>();
        _rig.Bus.Subscribe(handler);
        handler.Received.Clear();   // drop the replay: these tests look at what happens next
        return handler;
    }

    private RecordingHandler<T> Replayed<T>() where T : struct, IEvent
    {
        var handler = new RecordingHandler<T>();
        _rig.Bus.Subscribe(handler);
        return handler;
    }

    // ---- The starting picture, replayed to a late subscriber ------------------------------------

    [Fact]
    public void A_late_subscriber_is_replayed_an_idle_deck_with_no_track()
    {
        var content = Assert.Single(Replayed<DeckContent>().Received);

        Assert.Equal(0, content.Deck);
        Assert.Null(content.Track);
        Assert.Equal(DeckLoadState.Idle, content.LoadState);
        Assert.False(content.IsLoaded);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_the_tempo()
    {
        var tempo = Assert.Single(Replayed<DeckTempoChanged>().Received);

        Assert.Equal(0, tempo.Deck);
        Assert.Equal(0.0, tempo.SourceBpm);
        Assert.Equal(1.0, tempo.BpmMultiplier);
        Assert.Equal(_ports.Tempo.TempoRange, tempo.TempoRange);
        Assert.False(tempo.IsTempoShifted);
        Assert.False(tempo.WasMagnetAdjusted);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_no_loop()
    {
        var loop = Assert.Single(Replayed<DeckLoopChanged>().Received);

        Assert.Equal(new DeckLoopChanged(0, false, 0, 0), loop);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_a_closed_editor()
    {
        var edit = Assert.Single(Replayed<DeckEditChanged>().Received);

        Assert.Equal(new DeckEditChanged(0, false, false, false), edit);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_empty_markers()
    {
        var markers = Assert.Single(Replayed<DeckMarkersChanged>().Received);

        Assert.Equal(0, markers.Deck);
        Assert.Empty(markers.Markers);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_an_unmeasured_fader()
    {
        var mix = Assert.Single(Replayed<DeckMixChanged>().Received);

        Assert.Equal(new DeckMixChanged(0, false, 0, 0, false), mix);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_the_frame_at_rest()
    {
        var frame = Assert.Single(Replayed<DeckFrame>().Received);

        Assert.Equal(0, frame.Deck);
        Assert.Equal(0.0, frame.PlayPosition);
        Assert.Equal(0.0, frame.PlaybackSeconds);
        Assert.False(frame.IsScrubbing);
        Assert.False(frame.IsScratching);
        Assert.Equal(-1.0, frame.MagneticGlowSec);
    }

    // ---- Content --------------------------------------------------------------------------------

    [Fact]
    public void Beginning_a_load_publishes_the_track_in_the_loading_state()
    {
        var content = Watch<DeckContent>();

        _rig.Session.BeginLoad(_track);

        Assert.Contains(content.Received, c => c.LoadState == DeckLoadState.Loading);
        Assert.Same(_track, content.Received[^1].Track);
        Assert.Equal(DeckLoadState.Loading, content.Received[^1].LoadState);
    }

    [Fact]
    public void Loading_a_track_publishes_it_as_loaded()
    {
        var content = Watch<DeckContent>();

        _rig.Session.LoadTrack(_track, _track.FilePath, [], bpmMultiplier: 1.0);

        var last = content.Received[^1];
        Assert.Same(_track, last.Track);
        Assert.Equal(DeckLoadState.Loaded, last.LoadState);
        Assert.True(last.IsLoaded);
        Assert.Same(_rig.Session.Analysis, last.Analysis);
    }

    [Fact]
    public void Unloading_publishes_an_idle_deck_with_no_track()
    {
        _rig.Session.LoadTrack(_track, _track.FilePath, [], bpmMultiplier: 1.0);
        var content = Watch<DeckContent>();

        _rig.Session.Unload();

        var last = content.Received[^1];
        Assert.Null(last.Track);
        Assert.Equal(DeckLoadState.Idle, last.LoadState);
        Assert.False(last.IsLoaded);
    }

    [Fact]
    public void A_failed_load_publishes_the_failed_state()
    {
        _rig.Session.BeginLoad(_track);
        var content = Watch<DeckContent>();

        _rig.Session.LoadFailed();

        Assert.Equal(DeckLoadState.Failed, content.Received[^1].LoadState);
    }

    // ---- Markers, edit, loop --------------------------------------------------------------------

    [Fact]
    public void Setting_markers_publishes_them()
    {
        var markers = Watch<DeckMarkersChanged>();

        _rig.Session.SetMarkers(new[] { 1.5, 30.0 });

        Assert.Equal(new[] { 1.5, 30.0 }, Assert.Single(markers.Received).Markers);
    }

    [Fact]
    public void Toggling_the_editor_publishes_it_open_then_closed()
    {
        var edit = Watch<DeckEditChanged>();

        _rig.Session.ToggleEdit();
        Assert.True(edit.Received[^1].EditOpen);

        _rig.Session.ToggleEdit();
        Assert.False(edit.Received[^1].EditOpen);
    }

    [Fact]
    public void Toggling_grid_edit_publishes_the_grid_edit_flag()
    {
        var edit = Watch<DeckEditChanged>();

        _rig.Session.ToggleGridEdit();

        Assert.True(edit.Received[^1].GridEditActive);
    }

    // ---- Tempo ----------------------------------------------------------------------------------

    [Fact]
    public void Setting_the_tempo_range_publishes_it()
    {
        var tempo = Watch<DeckTempoChanged>();

        _rig.Session.SetTempoRange(0.16);

        Assert.Equal(0.16, tempo.Received[^1].TempoRange);
    }

    [Fact]
    public void Moving_the_tempo_fader_publishes_the_shifted_tempo_and_the_playback_speed_on_the_frame()
    {
        var tempo = Watch<DeckTempoChanged>();
        var frames = Watch<DeckFrame>();

        _rig.Session.SetTempoPosition(0.75);

        var last = tempo.Received[^1];
        Assert.True(last.IsTempoShifted);
        Assert.True(last.PlaybackSpeed > 1.0);
        Assert.Equal((double)_ports.Tempo.PlaybackSpeed, last.PlaybackSpeed);
        Assert.Equal((double)_ports.Tempo.PlaybackSpeed, frames.Received[^1].PlaybackSpeed);
    }

    [Fact]
    public void Halving_the_bpm_publishes_the_multiplier()
    {
        var tempo = Watch<DeckTempoChanged>();

        _rig.Session.HalveBpm();

        Assert.Equal(0.5, tempo.Received[^1].BpmMultiplier);
    }

    // ---- Mix ------------------------------------------------------------------------------------

    [Fact]
    public void Moving_the_channel_fader_publishes_a_known_gain()
    {
        var mix = Watch<DeckMixChanged>();

        _rig.Session.ChannelGain = 0.5;

        var last = mix.Received[^1];
        Assert.True(last.GainKnown);
        Assert.Equal(0.5, last.ChannelGain);
        Assert.Equal(0.5, last.EffectiveGain);
        Assert.False(last.IsMuted);
    }

    [Fact]
    public void A_fader_at_zero_publishes_muted()
    {
        var mix = Watch<DeckMixChanged>();

        _rig.Session.ChannelGain = 0;

        Assert.True(mix.Received[^1].IsMuted);
    }

    [Fact]
    public void The_crossfade_gain_is_part_of_the_published_effective_gain()
    {
        _rig.Session.ChannelGain = 0.5;
        var mix = Watch<DeckMixChanged>();

        _rig.Session.SetCrossfadeGain(0.5f);

        Assert.Equal(0.25, mix.Received[^1].EffectiveGain);
        Assert.Equal(0.5, mix.Received[^1].ChannelGain);
    }

    // ---- Frame ----------------------------------------------------------------------------------

    [Fact]
    public void Syncing_the_play_position_publishes_a_frame_with_the_position()
    {
        _ports.ScriptedPlayhead.PlayPosition = 0.4;
        _ports.ScriptedPlayhead.PositionFrames = 96_000;
        var frames = Watch<DeckFrame>();

        _rig.Session.SyncPlayPosition();

        var frame = frames.Received[^1];
        Assert.Equal(0, frame.Deck);
        Assert.Equal(0.4, frame.PlayPosition);
        Assert.True(frame.PlaybackSeconds > 0);
        Assert.Equal(_rig.Session.PlaybackSeconds, frame.PlaybackSeconds);
    }

    [Fact]
    public void Scrubbing_scratching_and_the_magnetic_glow_ride_on_the_frame()
    {
        var frames = Watch<DeckFrame>();

        _rig.Session.IsScrubbing = true;
        Assert.True(frames.Received[^1].IsScrubbing);

        _rig.Session.IsScratching = true;
        Assert.True(frames.Received[^1].IsScratching);

        _rig.Session.MagneticGlowSec = 3.5;
        Assert.Equal(3.5, frames.Received[^1].MagneticGlowSec);
        Assert.True(frames.Received[^1].IsScrubbing);
        Assert.True(frames.Received[^1].IsScratching);
    }

    [Fact]
    public void Setting_a_performance_flag_to_its_current_value_publishes_nothing()
    {
        var frames = Watch<DeckFrame>();

        _rig.Session.IsScrubbing = false;
        _rig.Session.IsScratching = false;
        _rig.Session.MagneticGlowSec = -1;

        Assert.Empty(frames.Received);
    }

    [Fact]
    public void Syncing_the_play_position_with_a_subscribed_frame_handler_allocates_nothing_after_warm_up()
    {
        var frames = new CountingEventHandler<DeckFrame>();
        _rig.Bus.Subscribe(frames);
        for (var i = 0; i < 1_000; i++)
        {
            _ports.ScriptedPlayhead.PlayPosition = (i % 100) / 100.0;
            _rig.Session.SyncPlayPosition();
        }
        var countBefore = frames.Count;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            _ports.ScriptedPlayhead.PlayPosition = (i % 100) / 100.0;
            _rig.Session.SyncPlayPosition();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(1_000, frames.Count - countBefore);
        Assert.Equal(0, allocated);
    }
}
