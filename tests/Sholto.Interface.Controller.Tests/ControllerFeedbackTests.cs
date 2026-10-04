using Sholto.Data;

namespace Sholto.Interface.Controller.Tests;

public class ControllerFeedbackTests
{
    private readonly RecordingControlSurface _surface = new();
    private readonly DataBus _bus = new(new ThrowingFailureSink());

    private ControllerFeedback Make()
    {
        var feedback = new ControllerFeedback(_surface, _bus);
        feedback.Subscribe();
        return feedback;
    }

    [Fact]
    public void Playing_lights_beat_sync_and_stopped_clears_it()
    {
        using var _ = Make();
        _bus.Publish(new DeckPlayStateChanged(1, PlayPhase.Playing, false));
        Assert.True(_surface.BeatSync[1]);
        Assert.False(_surface.BeatSync[0]);
        _bus.Publish(new DeckPlayStateChanged(1, PlayPhase.Stopped, false));
        Assert.False(_surface.BeatSync[1]);
    }

    [Fact]
    public void Ending_follows_the_flash_phase()
    {
        using var _ = Make();
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Ending, true));
        Assert.True(_surface.BeatSync[0]);
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Ending, false));
        Assert.False(_surface.BeatSync[0]);
    }

    [Fact]
    public void Stem_pad_is_lit_while_the_stem_is_audible()
    {
        using var _ = Make();
        _bus.Publish(new StemMuteChanged(1, 2, Muted: false));
        Assert.True(_surface.Pad[1, 2]);
        _bus.Publish(new StemMuteChanged(1, 2, Muted: true));
        Assert.False(_surface.Pad[1, 2]);
    }

    [Fact]
    public void Echo_event_drives_the_echo_light()
    {
        using var _ = Make();
        _bus.Publish(new EchoChanged(0, true));
        Assert.True(_surface.Echo[0]);
    }

    [Fact]
    public void Headphone_cue_event_lights_that_decks_cue_button()
    {
        using var _ = Make();
        _bus.Publish(new HeadphoneCueChanged(1, true));
        Assert.True(_surface.HeadphoneCue[1]);
        Assert.False(_surface.HeadphoneCue[0]);
        _bus.Publish(new HeadphoneCueChanged(1, false));
        Assert.False(_surface.HeadphoneCue[1]);
    }

    [Fact]
    public void Master_cue_event_lights_the_master_cue_button()
    {
        using var _ = Make();
        _bus.Publish(new MasterCueChanged(true));
        Assert.True(_surface.MasterCue);
    }

    [Fact]
    public void Pad_page_event_selects_that_decks_page_on_the_surface()
    {
        using var _ = Make();
        _bus.Publish(new PadPageChanged(1, PadPage.PadFx1));
        Assert.Equal(PadPage.PadFx1, _surface.Page[1]);
        Assert.Equal(PadPage.HotCue, _surface.Page[0]);
    }

    [Fact]
    public void Cue_and_pad_page_state_is_replayed_to_a_late_subscriber()
    {
        _bus.Publish(new HeadphoneCueChanged(0, true));
        _bus.Publish(new MasterCueChanged(true));
        _bus.Publish(new PadPageChanged(1, PadPage.PadFx1));
        using var _ = Make();
        Assert.True(_surface.HeadphoneCue[0]);
        Assert.True(_surface.MasterCue);
        Assert.Equal(PadPage.PadFx1, _surface.Page[1]);
    }

    [Fact]
    public void Reapply_repaints_cue_lights_and_pad_page_from_the_cache()
    {
        // Inspect closing: the App state never changed, so the cache repaints everything,
        // including a pad page the DJ pressed away from during Inspect.
        using var feedback = Make();
        _bus.Publish(new HeadphoneCueChanged(1, true));
        _bus.Publish(new MasterCueChanged(true));
        _bus.Publish(new PadPageChanged(0, PadPage.PadFx1));
        Array.Clear(_surface.HeadphoneCue);
        _surface.Page[0] = PadPage.HotCue;
        feedback.Reapply();
        Assert.True(_surface.HeadphoneCue[1]);
        Assert.True(_surface.MasterCue);
        Assert.Equal(PadPage.PadFx1, _surface.Page[0]);
    }

    [Fact]
    public void Leaving_inspect_repaints_every_light_from_the_cache()
    {
        using var _ = Make();
        _bus.Publish(new HeadphoneCueChanged(1, true));
        _bus.Publish(new PadPageChanged(0, PadPage.PadFx1));
        _bus.Publish(new InspectModeChanged(true));
        // Inspect: the DJ pressed HOT CUE on the unit; the App's page did not change.
        _surface.Page[0] = PadPage.HotCue;
        Array.Clear(_surface.HeadphoneCue);

        _bus.Publish(new InspectModeChanged(false));

        Assert.True(_surface.HeadphoneCue[1]);
        Assert.Equal(PadPage.PadFx1, _surface.Page[0]);
    }

    [Fact]
    public void Entering_inspect_writes_nothing_and_a_replayed_off_does_not_repaint()
    {
        using var feedback = Make();
        _bus.Publish(new InspectModeChanged(true));
        Assert.Equal(0, _surface.Writes);

        _bus.Publish(new InspectModeChanged(false));
        var afterClose = _surface.Writes;
        feedback.Resubscribe();   // the bus replays InspectModeChanged(false): not a transition

        Assert.True(afterClose > 0);
        Assert.Equal(afterClose, _surface.Writes);
    }

    [Fact]
    public void Resubscribe_replays_cue_state_a_dark_controller_lost()
    {
        using var feedback = Make();
        _bus.Publish(new HeadphoneCueChanged(0, true));
        _bus.Publish(new PadPageChanged(1, PadPage.PadFx1));
        Array.Clear(_surface.HeadphoneCue);
        _surface.Page[1] = PadPage.HotCue;
        feedback.Resubscribe();
        Assert.True(_surface.HeadphoneCue[0]);
        Assert.Equal(PadPage.PadFx1, _surface.Page[1]);
    }

    [Fact]
    public void Out_of_range_events_are_ignored()
    {
        using var _ = Make();
        _bus.Publish(new EchoChanged(5, true));
        _bus.Publish(new StemMuteChanged(0, 7, false));
        _bus.Publish(new HeadphoneCueChanged(3, true));
        _bus.Publish(new PadPageChanged(-1, PadPage.PadFx1));
        Assert.Equal(0, _surface.Writes);
    }

    [Fact]
    public void Subscribing_after_the_fact_replays_current_state()
    {
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Playing, false));
        _bus.Publish(new StemMuteChanged(1, 0, Muted: false));
        _bus.Publish(new EchoChanged(1, true));
        using var _ = Make();
        Assert.True(_surface.BeatSync[0]);
        Assert.True(_surface.Pad[1, 0]);
        Assert.True(_surface.Echo[1]);
    }

    [Fact]
    public void Resubscribe_replays_state_a_dark_controller_lost()
    {
        using var feedback = Make();
        _bus.Publish(new EchoChanged(0, true));
        _surface.Echo[0] = false; // the replugged device came up dark
        feedback.Resubscribe();
        Assert.True(_surface.Echo[0]);
    }

    [Fact]
    public void Resubscribe_does_not_double_deliver_live_events()
    {
        using var feedback = Make();
        feedback.Resubscribe();
        var before = _surface.Writes;
        _bus.Publish(new EchoChanged(0, true));
        Assert.Equal(before + 1, _surface.Writes);
    }

    [Fact]
    public void Reapply_rewrites_every_cached_light()
    {
        using var feedback = Make();
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Playing, false));
        _bus.Publish(new StemMuteChanged(0, 1, Muted: false));
        _bus.Publish(new EchoChanged(1, true));
        Array.Clear(_surface.BeatSync); Array.Clear(_surface.Pad); Array.Clear(_surface.Echo);
        feedback.Reapply();
        Assert.True(_surface.BeatSync[0]);
        Assert.True(_surface.Pad[0, 1]);
        Assert.True(_surface.Echo[1]);
        Assert.False(_surface.BeatSync[1]);
    }

    [Fact]
    public void Dispose_stops_delivery()
    {
        var feedback = Make();
        feedback.Dispose();
        _bus.Publish(new EchoChanged(0, true));
        Assert.False(_surface.Echo[0]);
    }

    [Fact]
    public void Publishing_a_feedback_event_allocates_nothing_after_warm_up()
    {
        using var _ = Make();
        for (var i = 0; i < 10; i++) PublishAll(i % 2 == 0);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) PublishAll(i % 2 == 0);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private void PublishAll(bool on)
    {
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Ending, on));
        _bus.Publish(new StemMuteChanged(1, 1, !on));
        _bus.Publish(new EchoChanged(1, on));
        _bus.Publish(new HeadphoneCueChanged(0, on));
        _bus.Publish(new MasterCueChanged(on));
        _bus.Publish(new PadPageChanged(1, on ? PadPage.PadFx1 : PadPage.HotCue));
    }

    private sealed class ThrowingFailureSink : IHandlerFailureSink
    {
        public void Report(in HandlerFailure failure) => throw failure.Exception;
    }
}
