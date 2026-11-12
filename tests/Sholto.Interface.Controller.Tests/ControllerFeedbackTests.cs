using Sholto.TestSupport.Allocation;
using Sholto.Data;
using Sholto.Interface.Controller.Model;

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

    private PadLight Pad(int deck, PadPage page, int pad) => _surface.Pads[deck, (int)page, pad];

    private void Audible(int deck, bool drums, bool vocals, bool instrumental)
    {
        _bus.Publish(new StemMuteChanged(deck, 0, Muted: !drums));
        _bus.Publish(new StemMuteChanged(deck, 1, Muted: !vocals));
        _bus.Publish(new StemMuteChanged(deck, 2, Muted: !instrumental));
    }

    [Fact]
    public void Sampler_mute_pads_are_lit_while_their_stem_is_audible()
    {
        using var _ = Make();
        _bus.Publish(new StemMuteChanged(1, 2, Muted: false));
        Assert.Equal(PadLight.On, Pad(1, PadPage.Sampler, 2));
        _bus.Publish(new StemMuteChanged(1, 2, Muted: true));
        Assert.Equal(PadLight.Off, Pad(1, PadPage.Sampler, 2));
    }

    [Fact]
    public void Sampler_mute_pads_of_audible_stems_are_lit_and_pads_four_and_eight_stay_dark()
    {
        using var _ = Make();
        Audible(0, drums: true, vocals: true, instrumental: true);

        Assert.Equal(PadLight.On, Pad(0, PadPage.Sampler, 0));
        Assert.Equal(PadLight.On, Pad(0, PadPage.Sampler, 1));
        Assert.Equal(PadLight.On, Pad(0, PadPage.Sampler, 2));
        Assert.Equal(PadLight.Off, Pad(0, PadPage.Sampler, 3));
        Assert.Equal(PadLight.Off, Pad(0, PadPage.Sampler, 7));
    }

    [Fact]
    public void Sampler_solo_pad_is_lit_only_while_its_stem_is_the_only_audible_one()
    {
        using var _ = Make();
        Audible(0, drums: true, vocals: true, instrumental: true);
        Assert.Equal(PadLight.Off, Pad(0, PadPage.Sampler, 5));

        Audible(0, drums: false, vocals: true, instrumental: false);

        Assert.Equal(PadLight.On, Pad(0, PadPage.Sampler, 5));
        Assert.Equal(PadLight.Off, Pad(0, PadPage.Sampler, 4));
        Assert.Equal(PadLight.Off, Pad(0, PadPage.Sampler, 6));
        Assert.Equal(PadLight.On, Pad(0, PadPage.Sampler, 1));   // the mute pad of an audible stem

        Audible(0, drums: false, vocals: true, instrumental: true);
        Assert.Equal(PadLight.Off, Pad(0, PadPage.Sampler, 5));
    }

    [Fact]
    public void Echo_event_drives_pad_one_of_the_pad_fx1_page()
    {
        using var _ = Make();
        _bus.Publish(new EchoChanged(0, true));
        Assert.Equal(PadLight.On, Pad(0, PadPage.PadFx1, 0));
        _bus.Publish(new EchoChanged(0, false));
        Assert.Equal(PadLight.Off, Pad(0, PadPage.PadFx1, 0));
    }

    [Fact]
    public void Subscribe_lights_roll_and_beat_jump_pads_without_Reapply()
    {
        using var _ = Make();

        for (var deck = 0; deck < 2; deck++)
        {
            Assert.Equal(PadLight.On, Pad(deck, PadPage.PadFx1, 1));
            for (var pad = 0; pad < 8; pad++)
                Assert.Equal(PadLight.On, Pad(deck, PadPage.BeatJump, pad));
        }
        for (var pad = 2; pad < 8; pad++)
            Assert.Equal(PadLight.Off, Pad(0, PadPage.PadFx1, pad));
    }

    [Fact]
    public void Hot_cue_pads_are_lit_whatever_the_cue_colour_and_empty_pads_stay_dark()
    {
        using var _ = Make();
        _bus.Publish(new DeckHotCuesChanged(1,
            [new HotCue(0, 4.0, CueColour.Pink), new HotCue(5, 9.0, CueColour.Green), new HotCue(7, 12.0, CueColour.Purple)]));

        Assert.Equal(PadLight.On, Pad(1, PadPage.HotCue, 0));
        Assert.Equal(PadLight.On, Pad(1, PadPage.HotCue, 5));
        Assert.Equal(PadLight.On, Pad(1, PadPage.HotCue, 7));
        Assert.Equal(PadLight.Off, Pad(1, PadPage.HotCue, 1));
        Assert.Equal(PadLight.Off, Pad(0, PadPage.HotCue, 0));
    }

    [Fact]
    public void Hot_cue_pads_clear_when_the_deck_reports_no_cues()
    {
        using var _ = Make();
        _bus.Publish(new DeckHotCuesChanged(0, [new HotCue(3, 4.0, CueColour.Red)]));
        _bus.Publish(new DeckHotCuesChanged(0, []));

        Assert.Equal(PadLight.Off, Pad(0, PadPage.HotCue, 3));
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
        var beforeResubscribe = _surface.Writes;
        feedback.Resubscribe();
        var resubscribeWrites = _surface.Writes - beforeResubscribe;   // the static pads, painted on every subscribe

        var beforeInspect = _surface.Writes;
        _bus.Publish(new InspectModeChanged(true));
        Assert.Equal(beforeInspect, _surface.Writes);

        _bus.Publish(new InspectModeChanged(false));
        var afterClose = _surface.Writes;
        feedback.Resubscribe();   // the bus replays InspectModeChanged(false): not a transition

        Assert.True(afterClose > beforeInspect);
        Assert.Equal(afterClose + resubscribeWrites, _surface.Writes);
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
        var before = _surface.Writes;
        _bus.Publish(new EchoChanged(5, true));
        _bus.Publish(new StemMuteChanged(0, 7, false));
        _bus.Publish(new HeadphoneCueChanged(3, true));
        _bus.Publish(new PadPageChanged(-1, PadPage.PadFx1));
        _bus.Publish(new DeckHotCuesChanged(4, [new HotCue(0, 1.0, CueColour.Red)]));
        Assert.Equal(before, _surface.Writes);
    }

    [Fact]
    public void Subscribing_after_the_fact_replays_current_state()
    {
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Playing, false));
        _bus.Publish(new StemMuteChanged(1, 0, Muted: false));
        _bus.Publish(new EchoChanged(1, true));
        using var _ = Make();
        Assert.True(_surface.BeatSync[0]);
        Assert.Equal(PadLight.On, Pad(1, PadPage.Sampler, 0));
        Assert.Equal(PadLight.On, Pad(1, PadPage.PadFx1, 0));
    }

    [Fact]
    public void Resubscribe_replays_state_a_dark_controller_lost()
    {
        using var feedback = Make();
        _bus.Publish(new EchoChanged(0, true));
        _bus.Publish(new DeckHotCuesChanged(0, [new HotCue(2, 8.0, CueColour.Blue)]));
        Array.Clear(_surface.Pads); // the replugged device came up dark
        feedback.Resubscribe();
        Assert.Equal(PadLight.On, Pad(0, PadPage.PadFx1, 0));
        Assert.Equal(PadLight.On, Pad(0, PadPage.PadFx1, 1));
        Assert.Equal(PadLight.On, Pad(0, PadPage.BeatJump, 7));
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
        _bus.Publish(new DeckHotCuesChanged(0, [new HotCue(6, 2.0, CueColour.Purple)]));
        Array.Clear(_surface.BeatSync); Array.Clear(_surface.Pads);
        feedback.Reapply();
        Assert.True(_surface.BeatSync[0]);
        Assert.Equal(PadLight.On, Pad(0, PadPage.Sampler, 1));
        Assert.Equal(PadLight.On, Pad(1, PadPage.PadFx1, 0));
        Assert.Equal(PadLight.On, Pad(0, PadPage.HotCue, 6));
        Assert.Equal(PadLight.On, Pad(1, PadPage.PadFx1, 1));
        Assert.Equal(PadLight.On, Pad(1, PadPage.BeatJump, 0));
        Assert.False(_surface.BeatSync[1]);
    }

    [Fact]
    public void Dispose_stops_delivery()
    {
        var feedback = Make();
        feedback.Dispose();
        _bus.Publish(new EchoChanged(0, true));
        Assert.Equal(PadLight.Off, Pad(0, PadPage.PadFx1, 0));
    }

    [Fact]
    public void Publishing_a_feedback_event_allocates_nothing_after_warm_up()
    {
        using var _ = Make();
        for (var i = 0; i < 10; i++) PublishAll(i % 2 == 0);

        var result = new AllocationWindows(5).Measure(() =>
        {
            for (var i = 0; i < 1000; i++) PublishAll(i % 2 == 0);
        });

        Assert.True(result.AllocatingWindows <= 1, result.ToString());
    }

    private void PublishAll(bool on)
    {
        _bus.Publish(new DeckPlayStateChanged(0, PlayPhase.Ending, on));
        _bus.Publish(new StemMuteChanged(1, 1, !on));
        _bus.Publish(new EchoChanged(1, on));
        _bus.Publish(new HeadphoneCueChanged(0, on));
        _bus.Publish(new MasterCueChanged(on));
        _bus.Publish(new PadPageChanged(1, on ? PadPage.PadFx1 : PadPage.HotCue));
        _bus.Publish(new DeckHotCuesChanged(0, _cues));
    }

    private readonly HotCue[] _cues = [new HotCue(1, 3.0, CueColour.Aqua)];

    private sealed class ThrowingFailureSink : IHandlerFailureSink
    {
        public void Report(in HandlerFailure failure) => throw failure.Exception;
    }
}
