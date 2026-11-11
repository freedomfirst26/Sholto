using Sholto.Data;
using Sholto.Interface.Controller.Model;

namespace Sholto.Interface.Controller.Mappings.DdjFlx4;

/// <summary>
/// Pioneer DDJ-FLX4 mapping. All channel numbers are raw MIDI wire values.
///
/// Channels observed:
///   1 / 2    Deck 1 / Deck 2 transport + jog wheels + channel faders
///   7        Top scroll-wheel cluster, crossfader, LOAD 1 / LOAD 2 buttons
///   11       Legacy big browse rotary on some firmwares
///
/// Every wire number lives in <see cref="DdjFlx4Options"/> (defaults match the
/// values documented above/inline) so this class holds only translation logic.
/// </summary>
public sealed class DdjFlx4Mapping : IControllerMapping
{
    private const int PadsPerPage = 8;
    private const int PadPageCount = 4;

    private readonly DdjFlx4Options _o;

    public DdjFlx4Mapping(DdjFlx4Options? options = null) => _o = options ?? new DdjFlx4Options();

    public string DeviceNameMatch => _o.DeviceNameMatch;

    public IReadOnlyList<string> AudioDeviceNameMatches => _o.AudioDeviceNameMatches;

    public ControllerEvent? Translate(NoteEvent msg)
    {
        // Browse / song-select needs both edges so the App can detect a long-press
        // (hold to re-analyze the highlighted track).
        if ((msg.Channel == _o.BrowsePressChannel && msg.Key == _o.BrowsePressNote) ||
            (msg.Channel == _o.BrowsePressLegacyChannel && msg.Key == _o.BrowsePressLegacyNote))
            return msg.IsDown ? new ControllerEvent.BrowsePressed() : new ControllerEvent.BrowseReleased();

        // Per-deck Shift modifier. Both edges matter — the App needs to know
        // which deck's Shift is currently held so it can route ambiguous
        // combined events (e.g. Shift+Left arrives as ch=5 0x4A regardless of
        // which deck modified it).
        if (msg.Key == _o.DeckShiftNote && (msg.Channel == _o.Deck0Channel || msg.Channel == _o.Deck1Channel))
            return new ControllerEvent.DeckShift(Deck: msg.Channel == _o.Deck0Channel ? 0 : 1, Pressed: msg.IsDown);

        // Top-platter touch. Both edges matter — the App scratches while the
        // hand is on and releases the instant it lifts.
        if (msg.Key == _o.JogTouchNote && (msg.Channel == _o.Deck0Channel || msg.Channel == _o.Deck1Channel))
            return new ControllerEvent.JogTouch(Deck: msg.Channel == _o.Deck0Channel ? 0 : 1, Touching: msg.IsDown);

        // Pads that need both edges: the HOT CUE pads (a paused jump plays while the pad is held) and
        // PAD FX1 pad 2 (roll, held rather than toggled like the echo on pad 1). These checks have to sit
        // above the press-only filter below.
        if (msg.Channel == _o.PadDeck0Channel || msg.Channel == _o.PadDeck1Channel)
        {
            int deck = msg.Channel == _o.PadDeck0Channel ? 0 : 1;
            if (msg.Key == _o.PadFx1NoteBase + 1)
                return new ControllerEvent.RollHold(deck, Pressed: msg.IsDown);

            int pad = msg.Key - _o.PadNoteBase;
            if ((uint)pad < PadsPerPage)
                return new ControllerEvent.HotCuePad(deck, pad, Shifted: false, Pressed: msg.IsDown);

            // Shift + pad arrives on its own note range; only the press means anything (it deletes the cue).
            int shiftedPad = msg.Key - _o.PadHotCueShiftNoteBase;
            if ((uint)shiftedPad < PadsPerPage)
                return msg.IsDown ? new ControllerEvent.HotCuePad(deck, shiftedPad, Shifted: true, Pressed: true) : null;
        }

        // Everything else is a press-only action — ignore the release edge so the
        // existing single-shot handlers don't fire twice per tap.
        if (!msg.IsDown) return null;

        if (msg.Channel == _o.Deck0Channel && msg.Key == _o.PlayNote)
            return new ControllerEvent.PlayPressed(Deck: 0);
        if (msg.Channel == _o.Deck1Channel && msg.Key == _o.PlayNote)
            return new ControllerEvent.PlayPressed(Deck: 1);

        // CUE transport button (above play/pause). Plain press used to
        // re-anchor the beatgrid to the playhead, which clobbered the grid on
        // every press — that binding was removed; grid editing now lives in
        // track-edit mode only, so plain CUE stays a no-op. Shift + CUE arrives
        // on its own note (ShiftCueNote) — the firmware remaps the chord — and
        // restarts the track. (CueNote below is the unrelated headphone-cue toggle.)
        if (msg.Key == _o.TransportCueNote || msg.Key == _o.ShiftCueNote)
        {
            bool shifted = msg.Key == _o.ShiftCueNote;
            if (msg.Channel == _o.Deck0Channel) return new ControllerEvent.TransportCuePressed(Deck: 0, Shifted: shifted);
            if (msg.Channel == _o.Deck1Channel) return new ControllerEvent.TransportCuePressed(Deck: 1, Shifted: shifted);
        }

        // Headphone CUE buttons (per deck) — toggle this deck into the
        // pre-fader headphone cue mix (output ch3-4). Master/cue blend is done
        // by the FLX-4 hardware MIXING knob, so we only track membership.
        if (msg.Channel == _o.Deck0Channel && msg.Key == _o.CueNote)
            return new ControllerEvent.CueToggle(Deck: 0);
        if (msg.Channel == _o.Deck1Channel && msg.Key == _o.CueNote)
            return new ControllerEvent.CueToggle(Deck: 1);

        // MASTER CUE — headphone monitor of master. The FLX-4 does the audio
        // blend in hardware; we route the press to the model only to light its LED.
        if (msg.Channel == _o.MasterCueChannel && msg.Key == _o.MasterCueNote)
            return new ControllerEvent.MasterCuePressed();

        // Top scroll-wheel cluster — per-deck LOAD buttons.
        if (msg.Channel == _o.LoadChannel && msg.Key == _o.LoadDeck0Note)
            return new ControllerEvent.LoadToDeck(Deck: 0);
        if (msg.Channel == _o.LoadChannel && msg.Key == _o.LoadDeck1Note)
            return new ControllerEvent.LoadToDeck(Deck: 1);

        // The press-only pads. Each page has its own note range on the same pad channel, so the pages
        // need no tracking here: BEAT JUMP pads jump by their beats, SAMPLER pads 1-3 mute and 5-7 solo
        // Drums / Vocals / Instrumental, PAD FX1 pad 1 toggles the echo. (HOT CUE and the roll pad are
        // matched above, since they need both edges.) The Controller tracks the active page only to know
        // which pad LEDs to repaint on a page switch (see Controller.SetPadPage).
        if (msg.Channel == _o.PadDeck0Channel || msg.Channel == _o.PadDeck1Channel)
        {
            int deck = msg.Channel == _o.PadDeck0Channel ? 0 : 1;

            int jumpPad = msg.Key - _o.PadBeatJumpNoteBase;
            if ((uint)jumpPad < PadsPerPage && jumpPad < _o.BeatJumpPadBeats.Length)
                return new ControllerEvent.BeatJumpPad(deck, jumpPad, _o.BeatJumpPadBeats[jumpPad]);

            int samplerPad = msg.Key - _o.PadSamplerNoteBase;
            if (samplerPad == _o.StemDrumsPad) return new ControllerEvent.StemToggle(deck, Group: 0);
            if (samplerPad == _o.StemVocalsPad) return new ControllerEvent.StemToggle(deck, Group: 1);
            if (samplerPad == _o.StemInstrumentalPad) return new ControllerEvent.StemToggle(deck, Group: 2);
            if (samplerPad == _o.StemSoloDrumsPad) return new ControllerEvent.StemSolo(deck, Group: 0);
            if (samplerPad == _o.StemSoloVocalsPad) return new ControllerEvent.StemSolo(deck, Group: 1);
            if (samplerPad == _o.StemSoloInstrumentalPad) return new ControllerEvent.StemSolo(deck, Group: 2);

            if (msg.Key == _o.PadFx1NoteBase) return new ControllerEvent.EchoToggle(deck);
        }

        // The four pad-mode buttons — switch the controller's own pad mode (on each deck's channel). Press
        // edge only; the Controller tracks per-deck page state and LEDs.
        if (msg.Channel == _o.Deck0Channel || msg.Channel == _o.Deck1Channel)
        {
            int deck = msg.Channel == _o.Deck0Channel ? 0 : 1;
            for (int page = 0; page < PadPageCount; page++)
                if (msg.Key == PadModeNote((PadPage)page))
                    return new ControllerEvent.PadPageSelected(deck, (PadPage)page);
        }

        // Beat-loop trio: 4 BEAT / EXIT, ½×, 2×. Deck0Channel = deck 0,
        // Deck1Channel = deck 1 on the FLX-4 (confirmed via raw MIDI capture).
        if (msg.Channel == _o.Deck0Channel && msg.Key == _o.BeatLoopToggleNote)
            return new ControllerEvent.BeatLoopToggle(Deck: 0, Bars: _o.BeatLoopToggleBars);
        if (msg.Channel == _o.Deck0Channel && msg.Key == _o.BeatLoopHalveNote)
            return new ControllerEvent.BeatLoopHalve(Deck: 0);
        if (msg.Channel == _o.Deck0Channel && msg.Key == _o.BeatLoopDoubleNote)
            return new ControllerEvent.BeatLoopDouble(Deck: 0);
        if (msg.Channel == _o.Deck1Channel && msg.Key == _o.BeatLoopToggleNote)
            return new ControllerEvent.BeatLoopToggle(Deck: 1, Bars: _o.BeatLoopToggleBars);
        if (msg.Channel == _o.Deck1Channel && msg.Key == _o.BeatLoopHalveNote)
            return new ControllerEvent.BeatLoopHalve(Deck: 1);
        if (msg.Channel == _o.Deck1Channel && msg.Key == _o.BeatLoopDoubleNote)
            return new ControllerEvent.BeatLoopDouble(Deck: 1);

        // Beatgrid nudge: dedicated FLX-4 buttons (captured via MIDI dump).
        // NudgeGridBackNote = nudge back 1 beat, NudgeGridForwardNote = nudge
        // forward. Deck = -1 → the App router picks whichever deck has an
        // active loop, or deck 0 as fallback.
        if (msg.Channel == _o.NudgeGridChannel && msg.Key == _o.NudgeGridBackNote)
            return new ControllerEvent.NudgeGrid(Deck: -1, Beats: -1);
        if (msg.Channel == _o.NudgeGridChannel && msg.Key == _o.NudgeGridForwardNote)
            return new ControllerEvent.NudgeGrid(Deck: -1, Beats: +1);

        // BEAT SYNC button (plain). BeatSyncNote = no Shift; the FLX-4 firmware
        // sends a DIFFERENT note (CycleTempoRangeNote) when Shift is held,
        // captured separately below — so we don't need to track Shift state
        // for this chord, the controller does it for us.
        if (msg.Channel == _o.Deck0Channel && msg.Key == _o.BeatSyncNote)
            return new ControllerEvent.BeatSyncPressed(Deck: 0);
        if (msg.Channel == _o.Deck1Channel && msg.Key == _o.BeatSyncNote)
            return new ControllerEvent.BeatSyncPressed(Deck: 1);

        // Shift + BEAT SYNC = cycle tempo range (Rekordbox TEMPO RANGE).
        if (msg.Channel == _o.Deck0Channel && msg.Key == _o.CycleTempoRangeNote)
            return new ControllerEvent.CycleTempoRange(Deck: 0);
        if (msg.Channel == _o.Deck1Channel && msg.Key == _o.CycleTempoRangeNote)
            return new ControllerEvent.CycleTempoRange(Deck: 1);

        return null;
    }

    public ControllerEvent? Translate(CcEvent msg)
    {
        // Crossfader: 14-bit value, MSB only (LSB ignored — 128 steps is enough).
        if (msg.Channel == _o.CrossfaderChannel && msg.Control == _o.CrossfaderControl)
            return new ControllerEvent.CrossfaderMoved(msg.Value / 127.0);

        // Per-deck channel volume faders: 14-bit, MSB on Deck0Channel / Deck1Channel.
        if (msg.Control == _o.ChannelVolumeControl && (msg.Channel == _o.Deck0Channel || msg.Channel == _o.Deck1Channel))
            return new ControllerEvent.ChannelVolumeMoved(msg.Channel == _o.Deck0Channel ? 0 : 1, msg.Value / 127.0);

        // Per-deck tempo fader: 14-bit, MSB only (LSB ignored; 128 steps is
        // plenty for pitch fader resolution). Pioneer convention: top of the
        // fader sends MSB = 0 (slow / negative pitch), bottom sends MSB = 127
        // (fast / positive pitch). Centre detent ≈ 64.
        if (msg.Control == _o.TempoControl && (msg.Channel == _o.Deck0Channel || msg.Channel == _o.Deck1Channel))
            return new ControllerEvent.TempoMoved(msg.Channel == _o.Deck0Channel ? 0 : 1, msg.Value / 127.0);

        // Per-deck COLOR / FILTER knob (14-bit, MSB only here — LSB is
        // ignored, 128 steps is plenty for a one-knob filter sweep). Both
        // knobs live on FilterChannel (NOT the per-deck channels). Center
        // detent sends val ≈ 64 → 0.5 → bypass.
        if (msg.Channel == _o.FilterChannel && (msg.Control == _o.FilterDeck0Control || msg.Control == _o.FilterDeck1Control))
        {
            int deck = msg.Control == _o.FilterDeck0Control ? 0 : 1;
            return new ControllerEvent.FilterMoved(deck, msg.Value / 127.0);
        }

        // Per-deck EQ pots (14-bit, MSB only here). FLX-4 sends HI/MID/LOW on
        // their own controls — Deck0Channel = Deck 1, Deck1Channel = Deck 2.
        if ((msg.Channel == _o.Deck0Channel || msg.Channel == _o.Deck1Channel) &&
            (msg.Control == _o.EqHighControl || msg.Control == _o.EqMidControl || msg.Control == _o.EqLowControl))
        {
            var band = msg.Control switch
            {
                var c when c == _o.EqHighControl => EqBand.High,
                var c when c == _o.EqMidControl  => EqBand.Mid,
                _                                => EqBand.Low,
            };
            return new ControllerEvent.EqMoved(msg.Channel == _o.Deck0Channel ? 0 : 1, band, msg.Value / 127.0);
        }

        // Top scroll-wheel rotation: signed 7-bit delta.
        //   val 1..63   →  positive (forward, e.g. val 1 = +1 tick)
        //   val 65..127 →  negative (back, two's-complement)
        if (msg.Channel == _o.BrowseRotateChannel && msg.Control == _o.BrowseRotateControl)
        {
            int delta = msg.Value < 64 ? msg.Value : msg.Value - 128;
            if (delta != 0) return new ControllerEvent.BrowseRotated(delta);
        }

        // Legacy big browse rotary (mixer section).
        if (msg.Channel == _o.BrowseRotateLegacyChannel && msg.Control == _o.BrowseRotateLegacyControl)
            return new ControllerEvent.BrowseRotated(msg.Value > 64 ? 1 : -1);

        // Jog wheel rotation. JogTopPlatterControl = top platter,
        // JogSideRingControl = side ring. Centered at JogCenter. While the
        // deck's Shift is held the firmware retransmits the top platter on
        // JogTopPlatterShiftControl (captured via MIDI dump) — same delta
        // encoding; surfaced as TopPlatter, and the App's shift state picks
        // the behaviour (4× fast search).
        if (msg.Control == _o.JogTopPlatterControl || msg.Control == _o.JogSideRingControl
            || msg.Control == _o.JogTopPlatterShiftControl)
        {
            int deck = msg.Channel == _o.Deck0Channel ? 0 : msg.Channel == _o.Deck1Channel ? 1 : -1;
            if (deck >= 0)
            {
                int delta = msg.Value - _o.JogCenter;
                if (delta != 0)
                {
                    var source = msg.Control == _o.JogSideRingControl ? JogSource.SideRing : JogSource.TopPlatter;
                    return new ControllerEvent.JogRotated(deck, delta, source);
                }
            }
        }

        return null;
    }

    /// <summary>Startup MIDI sequence that puts the FLX-4 pads into Hot Cue
    /// mode on both decks. Captured from the controller itself: pressing the
    /// HOT CUE button emits NoteOn/NoteOff on ch 1 (deck 1) or ch 2 (deck 2),
    /// note 0x1B. Sending the same bytes back asks the controller to enter
    /// that mode. Without this, Rekordbox may have left the pads in a
    /// layout we don't have mapped.
    /// Wire channel = displayed channel - 1, so:
    ///   Deck 1 displayed ch 1 → wire 0 → NoteOn status byte 0x90
    ///   Deck 2 displayed ch 2 → wire 1 → NoteOn status byte 0x91
    /// </summary>
    public byte[]? StartupInit() =>
        // NoteOn vel 0x7F then NoteOff (vel 0x00) for both decks.
        [
            0x90, 0x1B, 0x7F, 0x90, 0x1B, 0x00,  // Deck 1 HOT CUE press+release
            0x91, 0x1B, 0x7F, 0x91, 0x1B, 0x00,  // Deck 2 HOT CUE press+release
        ];

    /// <summary>The press simulation for a pad-mode button, as in <see cref="StartupInit"/>: NoteOn
    /// vel 0x7F then NoteOff on the deck's status byte, for the page's mode button (HOT CUE 0x1B, PAD FX1
    /// 0x1E, and the UNVERIFIED BEAT JUMP 0x20 and SAMPLER 0x22).</summary>
    public byte[]? RenderPadMode(int deck, PadPage page)
    {
        if (deck is not (0 or 1)) return null;
        int note = PadModeNote(page);
        if (note < 0) return null;
        byte status = (byte)(0x90 + deck);
        return [status, (byte)note, 0x7F, status, (byte)note, 0x00];
    }

    /// <summary>The note of a page's pad-mode button, or -1 for no such page.</summary>
    private int PadModeNote(PadPage page) => page switch
    {
        PadPage.HotCue => _o.PadModeHotCueNote,
        PadPage.PadFx1 => _o.PadModePadFx1Note,
        PadPage.BeatJump => _o.PadModeBeatJumpNote,
        PadPage.Sampler => _o.PadModeSamplerNote,
        _ => -1,
    };

    /// <summary>The note of pad 0 of a page.</summary>
    private int PadNoteBaseOf(PadPage page) => page switch
    {
        PadPage.HotCue => _o.PadNoteBase,
        PadPage.PadFx1 => _o.PadFx1NoteBase,
        PadPage.BeatJump => _o.PadBeatJumpNoteBase,
        _ => _o.PadSamplerNoteBase,
    };

    /// <summary>The velocity (colour) the hardware shows for a logical pad light.</summary>
    private byte PadVelocity(PadLight light) => light switch
    {
        PadLight.Off => _o.PadLightOffVelocity,
        >= PadLight.CuePink and <= PadLight.CuePurple => _o.CueVelocities[light - PadLight.CuePink],
        PadLight.Echo => _o.EchoVelocity,
        PadLight.Roll => _o.RollVelocity,
        PadLight.BeatJump => _o.BeatJumpVelocity,
        PadLight.Drums => _o.DrumsVelocity,
        PadLight.Vocals => _o.VocalsVelocity,
        PadLight.Instrumental => _o.InstrumentalVelocity,
        _ => _o.PadLightOffVelocity,
    };

    /// <summary>A pad LED — UNVERIFIED ON HARDWARE. We assume the same "echo the note" convention as
    /// Cue/BeatSync, on the pad's own NoteOn status byte (deck 1 pad channel 8 → 0x97, deck 2 pad channel
    /// 10 → 0x99), the velocity being the colour.</summary>
    public byte[]? RenderPadLight(int deck, PadPage page, int pad, PadLight light)
    {
        if (deck is not (0 or 1) || (uint)pad >= PadsPerPage || PadModeNote(page) < 0) return null;
        int channel = deck == 0 ? _o.PadDeck0Channel : _o.PadDeck1Channel;
        return [(byte)(_o.DeckLightStatusBase + channel - 1), (byte)(PadNoteBaseOf(page) + pad), PadVelocity(light)];
    }

    // --- Output / LEDs ---------------------------------------------------------
    // The FLX-4 lights a button by echoing its note back with velocity
    // ButtonLightOnVelocity (on) / ButtonLightOffVelocity (off): deck CUE on
    // the deck's channel (status DeckLightStatusBase + deck, note CueLightNote);
    // MASTER CUE on MasterCueLightStatus/-Note. Notes match the buttons' input
    // notes captured via MIDI dump.
    public byte[]? RenderLight(ControllerLight light, bool on)
    {
        byte vel = on ? _o.ButtonLightOnVelocity : _o.ButtonLightOffVelocity;
        return light.Function switch
        {
            LightFunction.Cue when light.Deck is 0 or 1
                => [(byte)(_o.DeckLightStatusBase + light.Deck), (byte)_o.CueLightNote, vel],
            LightFunction.MasterCue
                => [(byte)_o.MasterCueLightStatus, (byte)_o.MasterCueLightNote, vel],
            // BEAT SYNC LED — same note as the SYNC button press on the
            // per-deck output channel, mirroring the Cue light.
            LightFunction.BeatSync when light.Deck is 0 or 1
                => [(byte)(_o.DeckLightStatusBase + light.Deck), (byte)_o.BeatSyncLightNote, vel],
            // Pad-mode button LEDs — UNVERIFIED ON HARDWARE. We assume the
            // same "echo the input note back on the deck's own status byte"
            // convention as Cue/BeatSync; needs a real controller check.
            LightFunction.PadMode when light.Deck is 0 or 1 && PadModeNote((PadPage)light.Index) >= 0
                => [(byte)(_o.DeckLightStatusBase + light.Deck), (byte)PadModeNote((PadPage)light.Index), vel],
            _ => null,
        };
    }
}
