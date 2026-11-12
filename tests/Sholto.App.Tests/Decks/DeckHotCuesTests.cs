using Sholto.App.Decks;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The eight hot-cue pads of a deck through real sessions: set, jump, preview, delete, reload.</summary>
public class DeckHotCuesTests
{
    private readonly HotCueRig _rig = new();
    private readonly RecordingHandler<DeckHotCuesChanged> _published = new();

    public DeckHotCuesTests()
    {
        _rig.Bus.Subscribe(_published);
        _rig.HotCues.Attach(_rig.Store);
    }

    private HotCue[] Latest => _published.Received[^1].Cues;

    [Fact]
    public void Press_on_empty_pad_sets_a_cue_at_the_playhead_snapped_to_the_nearest_beat()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10.1);
        Assert.False(_rig.Deck1.SectionGrid.IsEmpty);
        _published.Received.Clear();

        _rig.HotCues.Press(0, 2, true);

        var cue = Assert.Single(Latest);
        Assert.Equal(2, cue.Pad);
        Assert.Equal(_rig.Deck1.SectionGrid.BeatAt(_rig.Deck1.SectionGrid.NearestBeatIndex(10.1)), cue.PositionSec, 6);
        Assert.Equal(10.0, cue.PositionSec, 6);
        Assert.Equal(CueColour.Green, cue.Colour);
    }

    [Fact]
    public void Snap_off_keeps_the_raw_playhead()
    {
        var rig = new HotCueRig(new HotCueOptions { SnapToBeat = false, DefaultColour = CueColour.Red });
        var seen = new RecordingHandler<DeckHotCuesChanged>();
        rig.Bus.Subscribe(seen);
        rig.HotCues.Attach(rig.Store);
        rig.Load(HotCueRig.Alpha, playheadSec: 10.1);

        rig.HotCues.Press(0, 0, true);

        Assert.Equal(new HotCue(0, 10.1, CueColour.Red), Assert.Single(seen.Received[^1].Cues));
    }

    [Fact]
    public void Press_on_empty_pad_with_no_track_loaded_does_nothing()
    {
        _published.Received.Clear();

        _rig.HotCues.Press(0, 0, true);

        Assert.Empty(_published.Received);
        Assert.Empty(_rig.Store.Calls);
    }

    [Fact]
    public void Press_before_Attach_does_nothing()
    {
        var rig = new HotCueRig();
        var seen = new RecordingHandler<DeckHotCuesChanged>();
        rig.Bus.Subscribe(seen);
        rig.Load(HotCueRig.Alpha, playheadSec: 3);

        rig.HotCues.Press(0, 0, true);
        rig.HotCues.Delete(0, 0);

        Assert.Empty(seen.Received);
        Assert.Empty(rig.Store.Calls);
    }

    [Fact]
    public void Press_on_set_pad_while_playing_jumps_and_keeps_playing()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 1, true);
        _rig.HotCues.Press(0, 1, false);
        _rig.Park(30);
        _rig.SetPlaying(true);

        _rig.HotCues.Press(0, 1, true);
        _rig.HotCues.Press(0, 1, false);

        Assert.Equal(-20, Assert.Single(_rig.Transports[0].Seeks), 6);
        Assert.True(_rig.Deck1.IsPlaying);
        Assert.Equal(0, _rig.Transports[0].TogglePlayCalls);
    }

    [Fact]
    public void Press_on_set_pad_while_paused_plays_from_the_cue_until_release()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 1, true);
        _rig.HotCues.Press(0, 1, false);
        _rig.Park(30);

        _rig.HotCues.Press(0, 1, true);

        Assert.True(_rig.Deck1.IsPlaying);
        Assert.Equal(10, _rig.Deck1.PlaybackSeconds, 6);

        _rig.Park(10.7);   // the deck has played on a little
        _rig.HotCues.Press(0, 1, false);

        Assert.False(_rig.Deck1.IsPlaying);
        Assert.Equal(10, _rig.Deck1.PlaybackSeconds, 6);
    }

    [Fact]
    public void Latch_during_a_preview_keeps_the_deck_playing_after_release_with_no_seek_back()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 1, true);
        _rig.HotCues.Press(0, 1, false);
        _rig.Park(30);
        _rig.HotCues.Press(0, 1, true);   // preview from the cue at 10 s
        _rig.Park(10.7);

        Assert.True(_rig.HotCues.LatchPreview(0));
        Assert.True(_rig.Deck1.IsPlaying);
        Assert.Equal(1, _rig.Transports[0].TogglePlayCalls);

        _rig.Park(11.4);   // played on while the pad is still held
        _rig.HotCues.Press(0, 1, false);

        Assert.True(_rig.Deck1.IsPlaying);
        Assert.Equal(1, _rig.Transports[0].TogglePlayCalls);
        Assert.Equal(11.4, _rig.Deck1.PlaybackSeconds, 6);
        Assert.Equal(-20, Assert.Single(_rig.Transports[0].Seeks), 6);   // only the jump to the cue
    }

    [Fact]
    public void After_a_latch_the_next_PLAY_press_pauses_as_usual()
    {
        var playback = new PlaybackRequests(new DeckPair(_rig.Deck1, _rig.Deck2), _rig.HotCues);
        var brakes = new List<int>();
        playback.BrakePauseRequested += brakes.Add;
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 1, true);
        _rig.HotCues.Press(0, 1, false);
        _rig.Park(30);
        _rig.HotCues.Press(0, 1, true);
        playback.OnPlayPressed(0);   // latches the preview
        _rig.HotCues.Press(0, 1, false);
        Assert.True(_rig.Deck1.IsPlaying);
        Assert.Empty(brakes);
        Assert.Equal(1, _rig.Transports[0].TogglePlayCalls);

        Assert.False(_rig.Deck1.Scratch.CanScratch);   // so the usual pause is a toggle (the brake is in PlaybackRequestsTests)

        playback.OnPlayPressed(0);
        _rig.Deck1.SyncPlayPosition();   // the session follows the transport once a frame

        Assert.Empty(brakes);
        Assert.False(_rig.Deck1.IsPlaying);
        Assert.Equal(2, _rig.Transports[0].TogglePlayCalls);
    }

    [Fact]
    public void LatchPreview_with_no_preview_in_progress_returns_false_and_changes_nothing()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 1, true);   // sets pad 1
        _rig.HotCues.Press(0, 1, false);
        _rig.SetPlaying(true);
        _rig.HotCues.Press(0, 1, true);   // jump while playing: not a preview

        Assert.False(_rig.HotCues.LatchPreview(0));
        Assert.False(_rig.HotCues.LatchPreview(1));
        Assert.True(_rig.Deck1.IsPlaying);
        Assert.Equal(0, _rig.Transports[0].TogglePlayCalls);
    }

    [Fact]
    public void Preview_without_a_latch_still_pauses_back_on_the_cue_after_another_deck_latched()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 1, true);
        _rig.HotCues.Press(0, 1, false);
        _rig.Park(30);
        _rig.HotCues.Press(0, 1, true);

        Assert.False(_rig.HotCues.LatchPreview(1));   // the other deck has no preview
        _rig.Park(10.7);
        _rig.HotCues.Press(0, 1, false);

        Assert.False(_rig.Deck1.IsPlaying);
        Assert.Equal(10, _rig.Deck1.PlaybackSeconds, 6);
    }

    [Fact]
    public void Release_of_a_pad_that_was_not_previewing_changes_nothing()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 1, true);   // sets pad 1
        _rig.HotCues.Press(0, 1, false);  // release after a set
        _rig.Park(30);
        _rig.HotCues.Press(0, 1, true);   // preview of pad 1 starts
        _rig.HotCues.Press(0, 4, true);   // sets pad 4 during the hold
        _rig.HotCues.Press(0, 4, false);

        Assert.True(_rig.Deck1.IsPlaying);
        Assert.Equal(1, _rig.Transports[0].TogglePlayCalls);
    }

    [Fact]
    public void Press_on_set_pad_never_overwrites_it()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 3, true);
        _rig.HotCues.Press(0, 3, false);
        _rig.Park(50);
        _rig.HotCues.Press(0, 3, true);
        _rig.HotCues.Press(0, 3, false);

        Assert.Equal(10, Assert.Single(Latest).PositionSec, 6);
        Assert.Equal(["set 3"], _rig.Store.Calls);
    }

    [Fact]
    public void Delete_clears_the_pad_and_publishes()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 3, true);

        _rig.HotCues.Delete(0, 3);

        Assert.Empty(Latest);
        Assert.Equal(["set 3", "delete 3"], _rig.Store.Calls);
    }

    [Fact]
    public void Delete_on_an_empty_pad_does_nothing()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _published.Received.Clear();

        _rig.HotCues.Delete(0, 5);

        Assert.Empty(_published.Received);
        Assert.Empty(_rig.Store.Calls);
    }

    [Fact]
    public void A_new_load_clears_the_cues_at_once_then_publishes_the_saved_ones()
    {
        _rig.Store.SetAsync(_rig.IdOf(HotCueRig.Bravo), new HotCue(2, 40, CueColour.Blue));
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 0, true);
        Assert.Single(Latest);

        _rig.Deck1.BeginLoad(HotCueRig.Bravo);
        Assert.Empty(Latest);

        _rig.Deck1.LoadTrack(HotCueRig.Bravo, HotCueRig.Bravo.FilePath, [], bpmMultiplier: 1.0);
        Assert.Equal([new HotCue(2, 40, CueColour.Blue)], Latest);
    }

    [Fact]
    public void Saved_cues_reload_when_the_same_track_is_loaded_again()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 0, true);

        _rig.Deck1.BeginLoad(HotCueRig.Bravo);
        _rig.Deck1.BeginLoad(HotCueRig.Alpha);
        _rig.Deck1.LoadTrack(HotCueRig.Alpha, HotCueRig.Alpha.FilePath, [], bpmMultiplier: 1.0);

        Assert.Equal(10, Assert.Single(Latest).PositionSec, 6);
    }

    [Fact]
    public void Set_and_delete_persist_through_the_store_and_refresh_the_marker_overlay()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);

        _rig.HotCues.Press(0, 6, true);
        Assert.Equal([new HotCue(6, 10, CueColour.Green)], Listed(_rig.Store.ListAsync(_rig.IdOf(HotCueRig.Alpha))));
        Assert.Equal([0], _rig.Markers.Refreshed);

        _rig.HotCues.Delete(0, 6);
        Assert.Empty(Listed(_rig.Store.ListAsync(_rig.IdOf(HotCueRig.Alpha))));
        Assert.Equal([0, 0], _rig.Markers.Refreshed);
    }

    [Fact]
    public void Cues_are_per_deck()
    {
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        _rig.HotCues.Press(0, 0, true);

        Assert.Equal(0, _published.Received[^1].Deck);
        _rig.HotCues.Press(1, 0, true);   // deck 2 has no track

        Assert.Equal(0, _published.Received[^1].Deck);
    }

    private static T Listed<T>(Task<T> task) => task.GetAwaiter().GetResult();
}
