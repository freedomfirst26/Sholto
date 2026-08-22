namespace Sholto.Controller.Mappings;

/// <summary>
/// Pioneer DDJ-FLX4 mapping. All channel numbers are raw MIDI wire values.
///
/// Channels observed:
///   1 / 2    Deck 1 / Deck 2 transport + jog wheels + channel faders
///   7        Top scroll-wheel cluster, crossfader, LOAD 1 / LOAD 2 buttons
///   11       Legacy big browse rotary on some firmwares
/// </summary>
public sealed class DdjFlx4Mapping : IControllerMapping
{
    public string DeviceNameMatch => "DDJ-FLX4";

    public ControllerEvent? Translate(NoteEvent msg)
    {
        // Browse / song-select needs both edges so the App can detect a long-press
        // (hold to re-analyze the highlighted track).
        if ((msg.Channel, msg.Key) is (7, 0x41) or (11, 0x15))
            return msg.IsDown ? new ControllerEvent.BrowsePressed() : new ControllerEvent.BrowseReleased();

        // Per-deck Shift modifier. Both edges matter — the App needs to know
        // which deck's Shift is currently held so it can route ambiguous
        // combined events (e.g. Shift+Left arrives as ch=5 0x4A regardless of
        // which deck modified it).
        if (msg.Key == 0x3F && (msg.Channel == 1 || msg.Channel == 2))
            return new ControllerEvent.DeckShift(Deck: msg.Channel - 1, Pressed: msg.IsDown);

        // Stem-level modifier (deck-agnostic). Hold this button to repurpose
        // the EQ knobs as per-stem attenuators. Both edges matter — the App
        // tracks the held state and switches CC routing in EqMoved.
        if (msg.Channel == 5 && msg.Key == 0x47)
            return new ControllerEvent.StemLevelMode(Pressed: msg.IsDown);

        // Everything else is a press-only action — ignore the release edge so the
        // existing single-shot handlers don't fire twice per tap.
        if (!msg.IsDown) return null;

        return (msg.Channel, msg.Key) switch
        {
            (1,  0x0B) => new ControllerEvent.PlayPressed(Deck: 0),
            (2,  0x0B) => new ControllerEvent.PlayPressed(Deck: 1),

            // CUE button → set beatgrid bar 1 at the current playhead.
            (1,  0x0C) => new ControllerEvent.SetDownbeatHere(Deck: 0),
            (2,  0x0C) => new ControllerEvent.SetDownbeatHere(Deck: 1),

            // Headphone CUE buttons (per deck, note 0x54) — toggle this deck into
            // the pre-fader headphone cue mix (output ch3-4). Master/cue blend is
            // done by the FLX-4 hardware MIXING knob, so we only track membership.
            (1,  0x54) => new ControllerEvent.CueToggle(Deck: 0),
            (2,  0x54) => new ControllerEvent.CueToggle(Deck: 1),

            // Top scroll-wheel cluster — per-deck LOAD buttons.
            (7,  0x46) => new ControllerEvent.LoadToDeck(Deck: 0),
            (7,  0x47) => new ControllerEvent.LoadToDeck(Deck: 1),

            // Hot Cue pads — pads 1/2/3 mute Drums / Vocals / Instrumental on the
            // matching deck. We assume the controller is in "Hot Cue" pad mode
            // (which is what Rekordbox leaves it in, and what we explicitly put
            // it into via SysEx at startup — see AlsaRawMidi). Hot Cue mode
            // emits notes 0x00-0x07 on each deck's wire channel.
            (8,  0x00) => new ControllerEvent.StemToggle(Deck: 0, Group: 0),
            (8,  0x01) => new ControllerEvent.StemToggle(Deck: 0, Group: 1),
            (8,  0x02) => new ControllerEvent.StemToggle(Deck: 0, Group: 2),
            (10, 0x00) => new ControllerEvent.StemToggle(Deck: 1, Group: 0),
            (10, 0x01) => new ControllerEvent.StemToggle(Deck: 1, Group: 1),
            (10, 0x02) => new ControllerEvent.StemToggle(Deck: 1, Group: 2),

            // Beat-loop trio: 4 BEAT / EXIT, ½×, 2×. Channel 1 = deck 0, channel
            // 2 = deck 1 on the FLX-4 (confirmed via raw MIDI capture).
            (1,  0x4D) => new ControllerEvent.BeatLoopToggle(Deck: 0, Bars: 4),
            (1,  0x51) => new ControllerEvent.BeatLoopHalve(Deck: 0),
            (1,  0x53) => new ControllerEvent.BeatLoopDouble(Deck: 0),
            (2,  0x4D) => new ControllerEvent.BeatLoopToggle(Deck: 1, Bars: 4),
            (2,  0x51) => new ControllerEvent.BeatLoopHalve(Deck: 1),
            (2,  0x53) => new ControllerEvent.BeatLoopDouble(Deck: 1),

            // Beatgrid nudge: dedicated FLX-4 buttons on channel 5 (captured
            // via MIDI dump). 0x4A = nudge back 1 beat, 0x4B = nudge forward.
            // Deck = -1 → the App router picks whichever deck has an active
            // loop, or deck 0 as fallback.
            (5,  0x4A) => new ControllerEvent.NudgeGrid(Deck: -1, Beats: -1),
            (5,  0x4B) => new ControllerEvent.NudgeGrid(Deck: -1, Beats: +1),

            // BEAT SYNC button (plain). 0x58 = no Shift; the FLX-4 firmware
            // sends a DIFFERENT note (0x60) when Shift is held, captured
            // separately below — so we don't need to track Shift state for
            // this chord, the controller does it for us.
            (1,  0x58) => new ControllerEvent.BeatSyncPressed(Deck: 0),
            (2,  0x58) => new ControllerEvent.BeatSyncPressed(Deck: 1),

            // Shift + BEAT SYNC = cycle tempo range (Rekordbox TEMPO RANGE).
            (1,  0x60) => new ControllerEvent.CyclePitchRange(Deck: 0),
            (2,  0x60) => new ControllerEvent.CyclePitchRange(Deck: 1),

            _ => null,
        };
    }

    public ControllerEvent? Translate(CcEvent msg)
    {
        // Crossfader: 14-bit value, ch 7, 0x1F = MSB (LSB at 0x3F ignored — 128 steps is enough).
        if (msg.Channel == 7 && msg.Control == 0x1F)
            return new ControllerEvent.CrossfaderMoved(msg.Value / 127.0);

        // Per-deck channel volume faders: 14-bit, 0x13 = MSB on ch 1 (Deck 1) / ch 2 (Deck 2).
        if (msg.Control == 0x13 && (msg.Channel == 1 || msg.Channel == 2))
            return new ControllerEvent.ChannelVolumeMoved(msg.Channel - 1, msg.Value / 127.0);

        // Per-deck tempo fader: 14-bit, 0x00 = MSB on ch 1/2 (LSB 0x20 ignored;
        // 128 steps is plenty for pitch fader resolution). Pioneer convention: top
        // of the fader sends MSB = 0 (slow / negative pitch), bottom sends MSB =
        // 127 (fast / positive pitch). Centre detent ≈ 64.
        if (msg.Control == 0x00 && (msg.Channel == 1 || msg.Channel == 2))
            return new ControllerEvent.TempoMoved(msg.Channel - 1, msg.Value / 127.0);

        // Per-deck COLOR / FILTER knob (14-bit, MSB only here — LSB at +0x20
        // is ignored, 128 steps is plenty for a one-knob filter sweep). Both
        // knobs live on channel 7 (NOT the per-deck channels): Deck 1 = 0x17,
        // Deck 2 = 0x18. Center detent sends val ≈ 64 → 0.5 → bypass.
        if (msg.Channel == 7 && (msg.Control == 0x17 || msg.Control == 0x18))
        {
            int deck = msg.Control == 0x17 ? 0 : 1;
            return new ControllerEvent.FilterMoved(deck, msg.Value / 127.0);
        }

        // Per-deck EQ pots (14-bit, MSB only here). FLX-4 sends HI on 0x07, MID on 0x0B,
        // LOW on 0x0F — channel 1 = Deck 1, channel 2 = Deck 2.
        if ((msg.Channel == 1 || msg.Channel == 2) &&
            (msg.Control == 0x07 || msg.Control == 0x0B || msg.Control == 0x0F))
        {
            var band = msg.Control switch
            {
                0x07 => EqBand.High,
                0x0B => EqBand.Mid,
                _    => EqBand.Low,
            };
            return new ControllerEvent.EqMoved(msg.Channel - 1, band, msg.Value / 127.0);
        }

        // Top scroll-wheel rotation: signed 7-bit delta.
        //   val 1..63   →  positive (forward, e.g. val 1 = +1 tick)
        //   val 65..127 →  negative (back, two's-complement)
        if (msg.Channel == 7 && msg.Control == 0x40)
        {
            int delta = msg.Value < 64 ? msg.Value : msg.Value - 128;
            if (delta != 0) return new ControllerEvent.BrowseRotated(delta);
        }

        // Legacy big browse rotary (mixer section).
        if (msg.Channel == 11 && msg.Control == 0x40)
            return new ControllerEvent.BrowseRotated(msg.Value > 64 ? 1 : -1);

        // Jog wheel rotation. 0x22 = top platter, 0x21 = side ring. Centered at 0x40.
        if (msg.Control == 0x22 || msg.Control == 0x21)
        {
            int deck = msg.Channel == 1 ? 0 : msg.Channel == 2 ? 1 : -1;
            if (deck >= 0)
            {
                int delta = msg.Value - 64;
                if (delta != 0)
                {
                    var source = msg.Control == 0x22 ? JogSource.TopPlatter : JogSource.SideRing;
                    return new ControllerEvent.JogRotated(deck, delta, source);
                }
            }
        }

        return null;
    }

    // --- Output / LEDs ---------------------------------------------------------
    // The FLX-4 lights a button by echoing its note back with velocity 0x7F (on)
    // / 0x00 (off) on the deck's channel — status 0x90 for Deck 1, 0x91 for
    // Deck 2. The LED notes match the buttons' input notes (captured via MIDI
    // dump): CUE 0x54, PLAY 0x0B, 4-BEAT loop 0x4D.
    public byte[]? RenderLight(ControllerLight light, bool on)
    {
        if (light.Deck is < 0 or > 1) return null;
        byte status = (byte)(0x90 + light.Deck);
        byte note = light.Function switch
        {
            LightFunction.Cue  => 0x54,
            LightFunction.Play => 0x0B,
            LightFunction.Loop => 0x4D,
            _ => 0,
        };
        if (note == 0) return null;
        return [status, note, on ? (byte)0x7F : (byte)0x00];
    }
}
