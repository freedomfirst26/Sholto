using Sholto.App;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

public class CueRoutingTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly RecordingMasterCueOutput _master = new();
    private readonly CoreStack _core;
    private readonly CueRouting _routing;

    public CueRoutingTests()
    {
        _core = new HeadlessCore().Core;
        _routing = new CueRouting(_core.Decks, _master, _bus);
    }

    [Fact]
    public void Start_publishes_everything_off_on_the_hot_cue_page_for_a_late_subscriber()
    {
        _routing.Start();
        var cue = new RecordingHandler<HeadphoneCueChanged>();
        var master = new RecordingHandler<MasterCueChanged>();
        var page = new RecordingHandler<PadPageChanged>();
        using var a = _bus.Subscribe(cue);
        using var b = _bus.Subscribe(master);
        using var c = _bus.Subscribe(page);

        Assert.Equal([new HeadphoneCueChanged(0, false), new HeadphoneCueChanged(1, false)], cue.Received);
        Assert.Equal([new MasterCueChanged(false)], master.Received);
        Assert.Equal([new PadPageChanged(0, PadPage.HotCue), new PadPageChanged(1, PadPage.HotCue)], page.Received);
    }

    [Fact]
    public void Toggling_headphone_cue_sets_that_decks_cue_and_publishes_each_flip()
    {
        var cue = new RecordingHandler<HeadphoneCueChanged>();
        using var a = _bus.Subscribe(cue);

        _routing.ToggleHeadphoneCue(1);
        Assert.True(_core.Decks.Deck2.CueActive);
        Assert.False(_core.Decks.Deck1.CueActive);

        _routing.ToggleHeadphoneCue(1);
        Assert.False(_core.Decks.Deck2.CueActive);
        Assert.Equal([new HeadphoneCueChanged(1, true), new HeadphoneCueChanged(1, false)], cue.Received);
    }

    [Fact]
    public void The_decks_cue_state_is_independent()
    {
        _routing.ToggleHeadphoneCue(0);
        _routing.ToggleHeadphoneCue(1);
        _routing.ToggleHeadphoneCue(0);
        Assert.False(_core.Decks.Deck1.CueActive);
        Assert.True(_core.Decks.Deck2.CueActive);
    }

    [Fact]
    public void Toggling_master_cue_drives_the_audio_output_and_publishes()
    {
        var master = new RecordingHandler<MasterCueChanged>();
        using var a = _bus.Subscribe(master);

        _routing.ToggleMasterCue();
        Assert.True(_master.Last);
        _routing.ToggleMasterCue();
        Assert.False(_master.Last);

        Assert.Equal([new MasterCueChanged(true), new MasterCueChanged(false)], master.Received);
    }

    [Fact]
    public void Selecting_a_pad_page_publishes_it_for_that_deck_only()
    {
        var page = new RecordingHandler<PadPageChanged>();
        using var a = _bus.Subscribe(page);

        _routing.SelectPadPage(1, PadPage.PadFx1);

        Assert.Equal([new PadPageChanged(1, PadPage.PadFx1)], page.Received);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_the_current_state_after_changes()
    {
        _routing.Start();
        _routing.ToggleHeadphoneCue(0);
        _routing.ToggleMasterCue();
        _routing.SelectPadPage(0, PadPage.PadFx1);

        var cue = new RecordingHandler<HeadphoneCueChanged>();
        var master = new RecordingHandler<MasterCueChanged>();
        var page = new RecordingHandler<PadPageChanged>();
        using var a = _bus.Subscribe(cue);
        using var b = _bus.Subscribe(master);
        using var c = _bus.Subscribe(page);

        Assert.Contains(new HeadphoneCueChanged(0, true), cue.Received);
        Assert.Equal([new MasterCueChanged(true)], master.Received);
        Assert.Contains(new PadPageChanged(0, PadPage.PadFx1), page.Received);
    }

    [Fact]
    public void Out_of_range_decks_are_ignored()
    {
        var cue = new RecordingHandler<HeadphoneCueChanged>();
        var page = new RecordingHandler<PadPageChanged>();
        using var a = _bus.Subscribe(cue);
        using var b = _bus.Subscribe(page);

        _routing.ToggleHeadphoneCue(5);
        _routing.SelectPadPage(-1, PadPage.PadFx1);

        Assert.Empty(cue.Received);
        Assert.Empty(page.Received);
    }

    [Fact]
    public void Master_cue_and_pad_page_changes_allocate_nothing_after_warm_up()
    {
        // The bus-publish part of the cue path. (A headphone toggle also pokes the deck's
        // view-model notification, which posts to the UI thread; that is not this path.)
        var counting = new RecordingMasterCueOutput();
        var routing = new CueRouting(_core.Decks, counting, _bus);
        for (var i = 0; i < 10; i++) Step(routing, i);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) Step(routing, i);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static void Step(CueRouting routing, int i)
    {
        routing.ToggleMasterCue();
        routing.SelectPadPage(i & 1, (i & 2) == 0 ? PadPage.HotCue : PadPage.PadFx1);
    }
}
