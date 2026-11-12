using Sholto.Data;
using Sholto.Interface.Controller.Gestures;
using Sholto.Interface.Controller.Mappings.DdjFlx4;
using Sholto.Interface.Controller.Model;

namespace Sholto.Interface.Controller.Tests;

/// <summary>The DDJ-FLX4's four Shift pages: Shift + HOT CUE = KEYBOARD (0x69), Shift + PAD FX1 = PAD FX2
/// (0x6B), Shift + BEAT JUMP = BEAT LOOP (0x6D), Shift + SAMPLER = KEY SHIFT (0x6F), each on the deck's own
/// channel. BEAT LOOP pads (0x60-0x67) loop 1/4 to 32 beats; the other three pages' pads (0x40, 0x50, 0x70)
/// do nothing and stay dark.</summary>
public class DdjFlx4ShiftPageTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly DdjFlx4Mapping _mapping = new();
    private readonly GestureRecognizer _recognizer = new();
    private readonly RecordingCommandSender _sender = new();

    private ControllerEvent? Down(int channel, int key) => _mapping.Translate(new NoteEvent(channel, key, 127, IsDown: true));
    private ControllerEvent? Up(int channel, int key) => _mapping.Translate(new NoteEvent(channel, key, 0, IsDown: false));

    /// <summary>A note through the whole controller input: mapping, recognizer, translator. Returns the
    /// commands it sent.</summary>
    private IReadOnlyList<object> Send(int channel, int key, bool down = true)
    {
        _sender.Sent.Clear();
        var evt = down ? Down(channel, key) : Up(channel, key);
        if (evt is not null && _recognizer.Recognize(evt, T0) is { } gesture)
            new GestureCommandTranslator(_sender, _recognizer).Translate(gesture);
        return [.. _sender.Sent];
    }

    // ---- J1: Shift + a mode button selects its Shift page ----------------------------------

    [Theory]
    [InlineData(1, 0x69, 0, PadPage.Keyboard)]
    [InlineData(2, 0x69, 1, PadPage.Keyboard)]
    [InlineData(1, 0x6B, 0, PadPage.PadFx2)]
    [InlineData(2, 0x6B, 1, PadPage.PadFx2)]
    [InlineData(1, 0x6D, 0, PadPage.BeatLoop)]
    [InlineData(2, 0x6D, 1, PadPage.BeatLoop)]
    [InlineData(1, 0x6F, 0, PadPage.KeyShift)]
    [InlineData(2, 0x6F, 1, PadPage.KeyShift)]
    public void Shift_and_a_mode_button_selects_its_shift_page_on_its_deck(int channel, int note, int deck, PadPage page)
    {
        Assert.Equal(new ControllerEvent.PadPageSelected(deck, page), Down(channel, note));
        Assert.Null(Up(channel, note));
    }

    [Theory]
    [InlineData(0x69, PadPage.Keyboard, "deck.padmode.hotcue")]
    [InlineData(0x6B, PadPage.PadFx2, "deck.padmode.padfx1")]
    [InlineData(0x6D, PadPage.BeatLoop, "deck.padmode.beatjump")]
    [InlineData(0x6F, PadPage.KeyShift, "deck.padmode.sampler")]
    public void A_shift_page_press_sends_select_pad_page_for_that_page_from_its_button(int note, PadPage page, string control)
    {
        var select = Assert.IsType<SelectPadPage>(Assert.Single(Send(2, note)));

        Assert.Equal((1, page), (select.Deck, select.Page));
        Assert.Equal(control, select.Origin.ControlId);
    }

    [Theory]
    [InlineData(0x1B, PadPage.HotCue)]
    [InlineData(0x1E, PadPage.PadFx1)]
    [InlineData(0x20, PadPage.BeatJump)]
    [InlineData(0x22, PadPage.Sampler)]
    public void The_plain_mode_buttons_still_select_their_own_pages(int note, PadPage page)
    {
        Assert.Equal(page, Assert.IsType<SelectPadPage>(Assert.Single(Send(1, note))).Page);
    }

    // ---- J2: BEAT LOOP pads loop their beats -----------------------------------------------

    [Theory]
    [InlineData(8, 0)]
    [InlineData(10, 1)]
    public void Beat_loop_pads_send_one_quarter_to_thirty_two_beats_in_order(int channel, int deck)
    {
        double[] beats = [0.25, 0.5, 1, 2, 4, 8, 16, 32];
        for (var pad = 0; pad < 8; pad++)
        {
            Assert.Equal(new ControllerEvent.BeatLoopPad(deck, pad, beats[pad]), Down(channel, 0x60 + pad));
            Assert.Null(Up(channel, 0x60 + pad));
        }
    }

    [Fact]
    public void Beat_loop_pad_one_toggles_a_quarter_beat_loop()
    {
        var loop = Assert.IsType<ToggleBeatLoop>(Assert.Single(Send(8, 0x60)));

        Assert.Equal(0, loop.Deck);
        Assert.Equal(0.25, loop.LengthBeats);
        Assert.Equal("deck.pad.1", loop.Origin.ControlId);
    }

    [Fact]
    public void Beat_loop_pad_eight_toggles_a_thirty_two_beat_loop()
    {
        var loop = Assert.IsType<ToggleBeatLoop>(Assert.Single(Send(10, 0x67)));

        Assert.Equal(1, loop.Deck);
        Assert.Equal(32, loop.LengthBeats);
        Assert.Equal("deck.pad.8", loop.Origin.ControlId);
    }

    [Fact]
    public void The_four_beat_button_still_toggles_whole_bars()
    {
        var loop = Assert.IsType<ToggleBeatLoop>(Assert.Single(Send(1, 0x4D)));

        Assert.Equal(4, loop.Bars);
        Assert.Equal(16, loop.LengthBeats);
        Assert.Equal("deck.beatloop.toggle", loop.Origin.ControlId);
    }

    // ---- J3: KEYBOARD, PAD FX2 and KEY SHIFT pads do nothing ---------------------------------

    [Theory]
    [InlineData(0x40)]   // KEYBOARD
    [InlineData(0x50)]   // PAD FX2
    [InlineData(0x70)]   // KEY SHIFT
    public void Pads_of_the_pages_with_no_job_send_no_command_on_either_edge_with_or_without_shift(int noteBase)
    {
        foreach (var channel in new[] { 8, 9, 10, 11 })
            for (var note = noteBase; note < noteBase + 8; note++)
            {
                Assert.Null(Down(channel, note));
                Assert.Null(Up(channel, note));
                Assert.Empty(Send(channel, note));
            }
    }

    [Fact]
    public void Feedback_lights_every_beat_loop_pad_and_darkens_every_pad_of_the_pages_with_no_job()
    {
        var surface = new RecordingControlSurface();
        for (var deck = 0; deck < 2; deck++)
            foreach (var page in new[] { PadPage.Keyboard, PadPage.PadFx2, PadPage.KeyShift })
                for (var pad = 0; pad < 8; pad++)
                    surface.SetPadLight(deck, page, pad, PadLight.On);   // whatever was there before
        using var feedback = new ControllerFeedback(surface, new DataBus(new ThrowingFailureSink()));

        feedback.Subscribe();

        for (var deck = 0; deck < 2; deck++)
            for (var pad = 0; pad < 8; pad++)
            {
                Assert.Equal(PadLight.On, surface.Pads[deck, (int)PadPage.BeatLoop, pad]);
                Assert.Equal(PadLight.Off, surface.Pads[deck, (int)PadPage.Keyboard, pad]);
                Assert.Equal(PadLight.Off, surface.Pads[deck, (int)PadPage.PadFx2, pad]);
                Assert.Equal(PadLight.Off, surface.Pads[deck, (int)PadPage.KeyShift, pad]);
            }
    }

    [Theory]
    [InlineData(PadPage.Keyboard, 0x40)]
    [InlineData(PadPage.PadFx2, 0x50)]
    [InlineData(PadPage.BeatLoop, 0x60)]
    [InlineData(PadPage.KeyShift, 0x70)]
    public void A_shift_pages_pad_lights_on_its_own_note(PadPage page, byte noteBase)
    {
        Assert.Equal([0x97, (byte)(noteBase + 2), 0x7F], _mapping.RenderPadLight(0, page, 2, PadLight.On));
        Assert.Equal([0x99, (byte)(noteBase + 7), 0x00], _mapping.RenderPadLight(1, page, 7, PadLight.Off));
    }

    // ---- J4: a page change relights the page buttons and repaints the pads -----------------

    [Theory]
    [InlineData(PadPage.Keyboard, 0x69)]
    [InlineData(PadPage.PadFx2, 0x6B)]
    [InlineData(PadPage.BeatLoop, 0x6D)]
    [InlineData(PadPage.KeyShift, 0x6F)]
    public void A_shift_pages_light_and_press_simulation_use_its_chord_note(PadPage page, byte note)
    {
        Assert.Equal([0x91, note, 0x7F], _mapping.RenderLight(new ControllerLight(1, LightFunction.PadMode, (int)page), true));
        Assert.Equal([0x90, note, 0x7F, 0x90, note, 0x00], _mapping.RenderPadMode(0, page));
    }

    [Fact]
    public void Changing_to_beat_loop_relights_the_page_buttons_and_repaints_the_beat_loop_pads()
    {
        var midi = new FakeMidiConnection();
        var controller = new Controller(midi);
        controller.Connect();
        var bus = new DataBus(new ThrowingFailureSink());
        using var live = new ControllerFeedback(controller, bus);
        live.Subscribe();
        bus.Publish(new PadPageChanged(0, PadPage.HotCue));
        midi.Sent.Clear();

        bus.Publish(new PadPageChanged(0, PadPage.BeatLoop));

        var cmp = new BytesComparer();
        Assert.Contains(new byte[] { 0x90, 0x6D, 0x7F }, midi.Sent, cmp);   // BEAT LOOP's light on
        Assert.Contains(new byte[] { 0x90, 0x1B, 0x00 }, midi.Sent, cmp);   // HOT CUE's light off
        Assert.Contains(new byte[] { 0x90, 0x6D, 0x7F, 0x90, 0x6D, 0x00 }, midi.Sent, cmp);   // hardware forced there
        for (var pad = 0; pad < 8; pad++)
            Assert.Contains(new byte[] { 0x97, (byte)(0x60 + pad), 0x7F }, midi.Sent, cmp);   // its pads, lit
        Assert.Equal([PadPage.BeatLoop],
            controller.Deck1PadModes.Select((b, i) => (b, i)).Where(t => t.b.IsLit).Select(t => (PadPage)t.i));
        Assert.DoesNotContain(controller.Deck2PadModes, b => b.IsLit);
    }

    [Fact]
    public void Changing_to_a_page_with_no_job_repaints_its_pads_dark()
    {
        var midi = new FakeMidiConnection();
        var controller = new Controller(midi);
        controller.Connect();
        var bus = new DataBus(new ThrowingFailureSink());
        using var feedback = new ControllerFeedback(controller, bus);
        feedback.Subscribe();
        midi.Sent.Clear();

        bus.Publish(new PadPageChanged(1, PadPage.KeyShift));

        var cmp = new BytesComparer();
        Assert.Contains(new byte[] { 0x91, 0x6F, 0x7F }, midi.Sent, cmp);
        for (var pad = 0; pad < 8; pad++)
            Assert.Contains(new byte[] { 0x99, (byte)(0x70 + pad), 0x00 }, midi.Sent, cmp);
        Assert.DoesNotContain(midi.Sent, b => b.Length == 3 && b[0] == 0x99 && b[1] is >= 0x70 and <= 0x77 && b[2] != 0x00);
    }

    [Fact]
    public void A_shift_page_press_on_the_wire_is_remembered_so_the_apps_matching_page_is_not_forced_again()
    {
        var midi = new FakeMidiConnection();
        var controller = new Controller(midi);
        controller.Connect();
        midi.Raise(new ControllerEvent.PadPageSelected(0, PadPage.BeatLoop));
        midi.Sent.Clear();

        controller.SetPadPage(0, PadPage.BeatLoop);

        Assert.DoesNotContain(new byte[] { 0x90, 0x6D, 0x7F, 0x90, 0x6D, 0x00 }, midi.Sent, new BytesComparer());
        Assert.Contains(new byte[] { 0x90, 0x6D, 0x7F }, midi.Sent, new BytesComparer());
    }

    [Fact]
    public void The_shift_page_options_keep_their_documented_defaults()
    {
        var o = new DdjFlx4Options();

        Assert.Equal((0x69, 0x6B, 0x6D, 0x6F), (o.PadModeKeyboardNote, o.PadModePadFx2Note, o.PadModeBeatLoopNote, o.PadModeKeyShiftNote));
        Assert.Equal((0x40, 0x50, 0x60, 0x70), (o.PadKeyboardNoteBase, o.PadFx2NoteBase, o.PadBeatLoopNoteBase, o.PadKeyShiftNoteBase));
        Assert.Equal([0.25, 0.5, 1, 2, 4, 8, 16, 32], o.BeatLoopPadBeats);
    }

    private sealed class ThrowingFailureSink : IHandlerFailureSink
    {
        public void Report(in HandlerFailure failure) => throw failure.Exception;
    }
}
