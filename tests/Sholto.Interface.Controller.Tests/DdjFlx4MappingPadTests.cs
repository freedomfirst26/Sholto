using Sholto.Data;
using Sholto.Interface.Controller.Mappings.DdjFlx4;
using Sholto.Interface.Controller.Model;

namespace Sholto.Interface.Controller.Tests;

/// <summary>The four pad pages of the DDJ-FLX4: the mode buttons, what each page's pads send, and the LED bytes
/// each pad gets. Pad channel 8 is deck 1 and 10 deck 2 (9 and 11 with Shift held); the deck channels are 1
/// and 2.</summary>
public class DdjFlx4MappingPadTests
{
    private readonly DdjFlx4Mapping _mapping = new();

    private ControllerEvent? Down(int channel, int key) => _mapping.Translate(new NoteEvent(channel, key, 127, IsDown: true));
    private ControllerEvent? Up(int channel, int key) => _mapping.Translate(new NoteEvent(channel, key, 0, IsDown: false));

    // ---- mode buttons -------------------------------------------------------------

    [Theory]
    [InlineData(1, 0x1B, 0, PadPage.HotCue)]
    [InlineData(2, 0x1E, 1, PadPage.PadFx1)]
    [InlineData(1, 0x20, 0, PadPage.BeatJump)]
    [InlineData(2, 0x20, 1, PadPage.BeatJump)]
    [InlineData(1, 0x22, 0, PadPage.Sampler)]
    [InlineData(2, 0x22, 1, PadPage.Sampler)]
    public void A_mode_button_press_selects_its_page_on_its_deck(int channel, int note, int deck, PadPage page)
    {
        Assert.Equal(new ControllerEvent.PadPageSelected(deck, page), Down(channel, note));
    }

    [Fact]
    public void A_mode_button_release_means_nothing()
    {
        Assert.Null(Up(1, 0x20));
        Assert.Null(Up(1, 0x22));
    }

    // ---- HOT CUE ------------------------------------------------------------------

    [Fact]
    public void Hot_cue_pads_send_both_edges()
    {
        Assert.Equal(new ControllerEvent.HotCuePad(0, 3, false, true), Down(8, 0x03));
        Assert.Equal(new ControllerEvent.HotCuePad(0, 3, false, false), Up(8, 0x03));
    }

    [Fact]
    public void Shift_hot_cue_pads_send_the_press_marked_shifted_and_no_release()
    {
        Assert.Equal(new ControllerEvent.HotCuePad(1, 3, true, true), Down(11, 0x03));
        Assert.Null(Up(11, 0x03));
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(11, 1)]
    public void Every_shift_hot_cue_pad_on_either_deck_is_a_shifted_press(int channel, int deck)
    {
        for (var pad = 0; pad < 8; pad++)
        {
            Assert.Equal(new ControllerEvent.HotCuePad(deck, pad, Shifted: true, Pressed: true), Down(channel, 0x00 + pad));
            Assert.Null(Up(channel, 0x00 + pad));
        }
    }

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    public void Shift_pads_of_the_other_pages_do_nothing_yet(int channel)
    {
        int[] bases = [0x10, 0x20, 0x30];
        foreach (var noteBase in bases)
            for (var note = noteBase; note < noteBase + 8; note++)
            {
                if (channel == 11 && note == 0x15) continue;   // the legacy browse press, pinned below
                Assert.Null(Down(channel, note));
                Assert.Null(Up(channel, note));
            }
    }

    [Fact]
    public void The_legacy_browse_press_keeps_its_place_on_the_deck_two_shift_pad_channel()
    {
        Assert.IsType<ControllerEvent.BrowsePressed>(Down(11, 0x15));
        Assert.IsType<ControllerEvent.BrowseReleased>(Up(11, 0x15));
    }

    [Fact]
    public void The_old_shift_note_range_on_the_pad_channels_is_gone()
    {
        for (var note = 0x08; note <= 0x0F; note++)
        {
            Assert.IsNotType<ControllerEvent.HotCuePad>(Down(8, note));
            Assert.IsNotType<ControllerEvent.HotCuePad>(Down(10, note));
            Assert.IsNotType<ControllerEvent.HotCuePad>(Up(8, note));
            Assert.IsNotType<ControllerEvent.HotCuePad>(Up(10, note));
        }
    }

    [Fact]
    public void The_first_hot_cue_pad_is_no_longer_a_stem_toggle()
    {
        Assert.IsNotType<ControllerEvent.StemToggle>(Down(8, 0x00));
        Assert.IsType<ControllerEvent.HotCuePad>(Down(8, 0x00));
    }

    // ---- PAD FX1 ------------------------------------------------------------------

    [Fact]
    public void Pad_fx1_pad_one_echoes_and_pad_two_rolls_on_both_edges()
    {
        Assert.Equal(new ControllerEvent.EchoHold(0, true), Down(8, 0x10));
        Assert.Equal(new ControllerEvent.EchoHold(0, false), Up(8, 0x10));
        Assert.Equal(new ControllerEvent.EchoHold(1, true), Down(10, 0x10));
        Assert.Equal(new ControllerEvent.EchoHold(1, false), Up(10, 0x10));
        Assert.Equal(new ControllerEvent.RollHold(1, true), Down(10, 0x11));
        Assert.Equal(new ControllerEvent.RollHold(1, false), Up(10, 0x11));
    }

    [Fact]
    public void Pad_fx1_pads_three_to_eight_do_nothing()
    {
        for (var note = 0x12; note <= 0x17; note++) Assert.Null(Down(8, note));
    }

    // ---- BEAT JUMP ----------------------------------------------------------------

    [Fact]
    public void Beat_jump_pads_send_their_beats()
    {
        double[] expected = [-1, 1, -2, 2, -4, 4, -8, 8];
        for (var pad = 0; pad < 8; pad++)
            Assert.Equal(new ControllerEvent.BeatJumpPad(0, pad, expected[pad]), Down(8, 0x20 + pad));
    }

    [Fact]
    public void Beat_jump_pad_six_on_deck_one_jumps_four_beats_forward()
    {
        Assert.Equal(new ControllerEvent.BeatJumpPad(0, 5, 4), Down(8, 0x25));
    }

    [Fact]
    public void Shift_beat_jump_pads_and_beat_jump_releases_do_nothing()
    {
        for (var note = 0x28; note <= 0x2F; note++) Assert.Null(Down(8, note));
        Assert.Null(Up(8, 0x25));
    }

    // ---- SAMPLER ------------------------------------------------------------------

    [Fact]
    public void Sampler_pads_one_to_three_toggle_the_stems()
    {
        Assert.Equal(new ControllerEvent.StemToggle(0, 0), Down(8, 0x30));
        Assert.Equal(new ControllerEvent.StemToggle(0, 1), Down(8, 0x31));
        Assert.Equal(new ControllerEvent.StemToggle(1, 2), Down(10, 0x32));
    }

    [Fact]
    public void Sampler_pads_five_to_seven_solo_the_stems()
    {
        Assert.Equal(new ControllerEvent.StemSolo(0, 0), Down(8, 0x34));
        Assert.Equal(new ControllerEvent.StemSolo(0, 1), Down(8, 0x35));
        Assert.Equal(new ControllerEvent.StemSolo(1, 2), Down(10, 0x36));
    }

    [Fact]
    public void Sampler_pads_four_and_eight_do_nothing()
    {
        Assert.Null(Down(8, 0x33));
        Assert.Null(Down(8, 0x37));
    }

    // ---- pad LEDs -----------------------------------------------------------------

    [Fact]
    public void A_lit_pad_is_velocity_7F_on_the_pads_note_on_the_pad_channels_status_byte()
    {
        Assert.Equal([0x97, 0x03, 0x7F], _mapping.RenderPadLight(0, PadPage.HotCue, 3, PadLight.On));
        Assert.Equal([0x99, 0x11, 0x7F], _mapping.RenderPadLight(1, PadPage.PadFx1, 1, PadLight.On));
        Assert.Equal([0x97, 0x27, 0x7F], _mapping.RenderPadLight(0, PadPage.BeatJump, 7, PadLight.On));
        Assert.Equal([0x99, 0x34, 0x7F], _mapping.RenderPadLight(1, PadPage.Sampler, 4, PadLight.On));
    }

    [Fact]
    public void A_dark_pad_is_velocity_zero()
    {
        Assert.Equal([0x97, 0x30, 0x00], _mapping.RenderPadLight(0, PadPage.Sampler, 0, PadLight.Off));
        Assert.Equal([0x99, 0x07, 0x00], _mapping.RenderPadLight(1, PadPage.HotCue, 7, PadLight.Off));
    }

    [Fact]
    public void An_out_of_range_pad_or_deck_has_no_light()
    {
        Assert.Null(_mapping.RenderPadLight(2, PadPage.HotCue, 0, PadLight.On));
        Assert.Null(_mapping.RenderPadLight(0, PadPage.HotCue, 8, PadLight.On));
    }

    [Fact]
    public void Mode_button_lights_echo_the_mode_note_on_the_deck_status_byte()
    {
        Assert.Equal([0x90, 0x20, 0x7F], _mapping.RenderLight(new ControllerLight(0, LightFunction.PadMode, (int)PadPage.BeatJump), true));
        Assert.Equal([0x91, 0x22, 0x00], _mapping.RenderLight(new ControllerLight(1, LightFunction.PadMode, (int)PadPage.Sampler), false));
        Assert.Equal([0x91, 0x1B, 0x7F], _mapping.RenderLight(new ControllerLight(1, LightFunction.PadMode, (int)PadPage.HotCue), true));
    }

    // ---- options pinned -----------------------------------------------------------

    [Fact]
    public void The_pad_options_keep_their_documented_defaults()
    {
        var o = new DdjFlx4Options();

        Assert.Equal((0x1B, 0x1E, 0x20, 0x22), (o.PadModeHotCueNote, o.PadModePadFx1Note, o.PadModeBeatJumpNote, o.PadModeSamplerNote));
        Assert.Equal((8, 10), (o.PadDeck0Channel, o.PadDeck1Channel));
        Assert.Equal((9, 11), (o.PadShiftDeck0Channel, o.PadShiftDeck1Channel));
        Assert.Equal((0x00, 0x10, 0x20, 0x30), (o.PadNoteBase, o.PadFx1NoteBase, o.PadBeatJumpNoteBase, o.PadSamplerNoteBase));
        Assert.Equal([-1.0, 1, -2, 2, -4, 4, -8, 8], o.BeatJumpPadBeats);
        Assert.Equal((0, 1, 2), (o.StemDrumsPad, o.StemVocalsPad, o.StemInstrumentalPad));
        Assert.Equal((4, 5, 6), (o.StemSoloDrumsPad, o.StemSoloVocalsPad, o.StemSoloInstrumentalPad));
        Assert.Equal<byte>(0x7F, o.PadLightOnVelocity);
        Assert.Equal<byte>(0x00, o.PadLightOffVelocity);
    }
}
