using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>Hot-cue correctness against stores that hold or fail their calls. These are synchronous on purpose:
/// the awaited store tasks resume inline on the test thread when the test releases them, so no sleeps are needed.</summary>
public class DeckHotCuesCorrectnessTests
{
    private static readonly HotCue Saved = new(0, 30.0, CueColour.Red);

    private readonly HotCueRig _rig = new();
    private readonly RecordingHandler<DeckHotCuesChanged> _seen = new();

    public DeckHotCuesCorrectnessTests() => _rig.Bus.Subscribe(_seen);

    /// <summary>The hot-cue lines written while <paramref name="act"/> ran.</summary>
    private LoggedLine[] LinesDuring(Action act)
    {
        var before = _rig.Logs.Lines.Count;
        act();
        return _rig.Logs.Lines.Skip(before).Where(l => l.Tag == "HotCue").ToArray();
    }

    [Fact]
    public void Press_before_the_saved_cues_have_loaded_is_ignored_and_never_overwrites_them()
    {
        var store = new GatedHotCueStore();
        var id = _rig.IdOf(HotCueRig.Alpha);
        store.Seed(id, Saved);
        store.HoldLists = true;
        _rig.HotCues.Attach(store);
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);

        _rig.HotCues.Press(0, 0, true);
        _rig.HotCues.Press(0, 0, false);
        Assert.DoesNotContain("set 0", store.Calls);

        store.ReleaseLists();

        Assert.Equal(Saved, store.Committed(id, 0));
        Assert.Equal(Saved, Assert.Single(_seen.Received[^1].Cues));
    }

    [Fact]
    public void Press_after_the_saved_cues_have_loaded_jumps_instead_of_setting()
    {
        var store = new GatedHotCueStore();
        store.Seed(_rig.IdOf(HotCueRig.Alpha), Saved);
        store.HoldLists = true;
        _rig.HotCues.Attach(store);
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        store.ReleaseLists();

        _rig.HotCues.Press(0, 0, true);

        Assert.DoesNotContain("set 0", store.Calls);
        Assert.Equal(20, Assert.Single(_rig.Transports[0].Seeks), 6);
    }

    [Fact]
    public void A_deck_already_loaded_when_the_store_attaches_gets_its_cues_and_pads_work()
    {
        var store = new GatedHotCueStore();
        var id = _rig.IdOf(HotCueRig.Alpha);
        store.Seed(id, Saved);
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);

        _rig.HotCues.Attach(store);
        _rig.HotCues.Press(0, 0, true);

        Assert.Equal(Saved, Assert.Single(_seen.Received[^1].Cues));
        Assert.DoesNotContain("set 0", store.Calls);
        _rig.HotCues.Press(0, 1, true);
        Assert.Contains("set 1", store.Calls);
    }

    [Fact]
    public void A_failed_save_and_delete_are_logged_and_later_writes_still_run()
    {
        var store = new ThrowingHotCueStore(listFails: false);
        _rig.HotCues.Attach(store);
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);

        var lines = LinesDuring(() =>
        {
            _rig.HotCues.Press(0, 0, true);
            _rig.HotCues.Press(0, 0, false);
            _rig.HotCues.Delete(0, 0);
        });

        Assert.Single(lines, l => l.Level == LogLevel.Error && l.Message.StartsWith("save failed"));
        Assert.Single(lines, l => l.Level == LogLevel.Error && l.Message.StartsWith("delete failed"));
        Assert.Equal(["set 0", "delete 0"], store.Calls);
    }

    [Fact]
    public void A_failed_load_is_logged_and_the_pads_stay_locked()
    {
        var store = new ThrowingHotCueStore(listFails: true);
        _rig.HotCues.Attach(store);

        var lines = LinesDuring(() =>
        {
            _rig.Load(HotCueRig.Alpha, playheadSec: 10);
            _rig.HotCues.Press(0, 0, true);
        });

        Assert.Single(lines, l => l.Level == LogLevel.Error && l.Message.StartsWith("load failed"));
        Assert.Empty(store.Calls);
    }

    [Fact]
    public void A_quick_set_then_delete_leaves_no_cue_saved()
    {
        var store = new GatedHotCueStore();
        var id = _rig.IdOf(HotCueRig.Alpha);
        _rig.HotCues.Attach(store);
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        store.HoldWrites = true;

        _rig.HotCues.Press(0, 2, true);
        _rig.HotCues.Press(0, 2, false);
        _rig.HotCues.Delete(0, 2);
        store.ReleaseWrites();

        Assert.Null(store.Committed(id, 2));
        Assert.Empty(_seen.Received[^1].Cues);
    }

    [Fact]
    public void A_load_waits_for_writes_made_before_it()
    {
        var store = new GatedHotCueStore();
        var id = _rig.IdOf(HotCueRig.Alpha);
        _rig.HotCues.Attach(store);
        _rig.Load(HotCueRig.Alpha, playheadSec: 10);
        store.HoldWrites = true;
        _rig.HotCues.Press(0, 3, true);
        _rig.HotCues.Press(0, 3, false);

        _rig.Load(HotCueRig.Alpha, playheadSec: 10);   // reloaded before the save committed
        store.ReleaseWrites();

        Assert.Equal(3, Assert.Single(_seen.Received[^1].Cues).Pad);
        Assert.NotNull(store.Committed(id, 3));
    }
}
