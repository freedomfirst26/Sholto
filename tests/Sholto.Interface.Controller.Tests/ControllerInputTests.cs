using Sholto.Data;

namespace Sholto.Interface.Controller.Tests;

public class ControllerInputTests
{
    private readonly EmittingControlSurface _surface = new();
    private readonly RecordingCommandSender _sender = new();
    private readonly QueuedAppThread _thread = new();
    private readonly ManualFrameClock _clock = new();

    public ControllerInputTests() =>
        new ControllerInputFactory().Create(_surface, _sender, _thread, _clock).Start();

    [Fact]
    public void An_event_is_posted_to_the_app_thread_and_sends_nothing_until_it_runs()
    {
        _surface.Emit(new ControllerEvent.PlayPressed(1));

        Assert.Equal(1, _thread.PostedCount);
        Assert.Empty(_sender.Sent);

        _thread.RunPosted();

        var play = Assert.IsType<TogglePlay>(Assert.Single(_sender.Sent));
        Assert.Equal(1, play.Deck);
        Assert.Equal(new Origin(InterfaceIds.Controller, "deck.play", "play.press", 1), play.Origin);
    }

    [Fact]
    public void Events_arrive_in_the_order_they_were_emitted()
    {
        _surface.Emit(new ControllerEvent.PlayPressed(0));
        _surface.Emit(new ControllerEvent.CueToggle(1));
        _surface.Emit(new ControllerEvent.MasterCuePressed());
        _thread.RunPosted();

        Assert.IsType<TogglePlay>(_sender.Sent[0]);
        Assert.IsType<ToggleHeadphoneCue>(_sender.Sent[1]);
        Assert.IsType<ToggleMasterCue>(_sender.Sent[2]);
    }

    [Fact]
    public void A_modifier_hold_is_sent_as_a_report_with_its_deck_and_edge()
    {
        _surface.Emit(new ControllerEvent.DeckShift(1, Pressed: true));
        _surface.Emit(new ControllerEvent.DeckShift(1, Pressed: false));
        _thread.RunPosted();

        var held = Assert.IsType<ReportControl>(_sender.Sent[0]);
        Assert.Equal((true, new Origin(InterfaceIds.Controller, "deck.shift", "shift.hold", 1)),
            (held.Pressed, held.Origin));
        var released = Assert.IsType<ReportControl>(_sender.Sent[1]);
        Assert.Equal((false, new Origin(InterfaceIds.Controller, "deck.shift", "shift.release", 1)),
            (released.Pressed, released.Origin));
    }

    [Fact]
    public void The_controller_always_sends_whatever_the_apps_mode_so_shift_tracking_never_lapses()
    {
        _surface.Emit(new ControllerEvent.DeckShift(0, Pressed: true));
        _thread.RunPosted();

        _surface.Emit(new ControllerEvent.JogTouch(0, Touching: true));
        _thread.RunPosted();

        Assert.True(Assert.IsType<TouchPlatter>(_sender.Sent[^1]).Shifted);
    }

    [Fact]
    public void The_browse_hold_is_ticked_on_the_frame_clock_and_sends_reanalyse_after_a_second()
    {
        _surface.Emit(new ControllerEvent.BrowsePressed());
        _thread.RunPosted();
        _clock.Tick();
        Assert.Empty(_sender.Sent);

        // The hold is wall-clock timed from the press; a frame a second later makes it due.
        Thread.Sleep(1050);
        _clock.Tick();

        var hold = Assert.IsType<ReanalyzeSelected>(Assert.Single(_sender.Sent));
        Assert.Equal("browse.press.hold", hold.Origin.GestureName);
    }

    [Fact]
    public void A_short_browse_press_reports_the_control_once_on_release()
    {
        _surface.Emit(new ControllerEvent.BrowsePressed());
        _thread.RunPosted();
        _clock.Tick();
        _surface.Emit(new ControllerEvent.BrowseReleased());
        _thread.RunPosted();

        var report = Assert.IsType<ReportControl>(Assert.Single(_sender.Sent));
        Assert.Equal(new Origin(InterfaceIds.Controller, "browse.knob", "browse.press.short"), report.Origin);
    }

    [Fact]
    public void The_hold_sends_once_and_releasing_it_sends_nothing_more()
    {
        _surface.Emit(new ControllerEvent.BrowsePressed());
        _thread.RunPosted();
        Thread.Sleep(1050);
        _clock.Tick();
        _clock.Tick();
        _surface.Emit(new ControllerEvent.BrowseReleased());
        _thread.RunPosted();

        Assert.Single(_sender.Sent);
    }
}
