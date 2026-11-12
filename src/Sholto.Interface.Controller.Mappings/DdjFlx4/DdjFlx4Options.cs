namespace Sholto.Interface.Controller.Mappings.DdjFlx4;

/// <summary>
/// Every raw MIDI wire number <see cref="DdjFlx4Mapping"/> needs to talk to a
/// Pioneer DDJ-FLX4. Extracted out of the mapping's switch logic so the wire
/// numbers are named and tunable (e.g. from config) without touching the
/// mapping's control-flow. Every default reproduces the mapping's original
/// (pre-refactor) hardcoded value exactly, so behavior is unchanged with a
/// fresh <c>new DdjFlx4Options()</c>.
///
/// Channel/note/CC values are raw wire numbers (1-16 for channel, matching what
/// <see cref="MidiManager"/> hands to mappings — see its OnRawMidi comment).
/// </summary>
public sealed class DdjFlx4Options
{
    /// <summary>Substring that identifies this controller in /proc/asound/cards.</summary>
    public string DeviceNameMatch { get; set; } = "DDJ-FLX4";

    /// <summary>Substrings that identify the controller's built-in sound card in
    /// playback-device and PipeWire sink names. Matches on "DDJ-FLX4" or
    /// "AlphaTheta" (the manufacturer) rather than a full node name — the node
    /// name embeds the unit's serial number, which differs per controller.</summary>
    public string[] AudioDeviceNameMatches { get; set; } = ["DDJ-FLX4", "AlphaTheta"];

    // --- Per-deck channel base --------------------------------------------------
    // Most per-deck controls (transport, jog, EQ, faders, pads) live on a
    // dedicated wire channel per deck. Deck index 0 = Deck 1, 1 = Deck 2.
    public int Deck0Channel { get; set; } = 1;
    public int Deck1Channel { get; set; } = 2;

    // --- Notes: Browse / Shift / Stem-level mode --------------------------------
    public int BrowsePressChannel { get; set; } = 7;
    public int BrowsePressNote { get; set; } = 0x41;
    /// <summary>Legacy big browse rotary's press, on some firmwares.</summary>
    public int BrowsePressLegacyChannel { get; set; } = 11;
    public int BrowsePressLegacyNote { get; set; } = 0x15;

    /// <summary>Per-deck Shift modifier note (sent on <see cref="Deck0Channel"/> / <see cref="Deck1Channel"/>).</summary>
    public int DeckShiftNote { get; set; } = 0x3F;

    // --- Notes: transport --------------------------------------------------------
    public int PlayNote { get; set; } = 0x0B;
    /// <summary>CUE transport button above PLAY (per deck channel). Plain press is
    /// unmapped; Shift + CUE restarts the track from the start.</summary>
    public int TransportCueNote { get; set; } = 0x0C;

    /// <summary>Shift + CUE — like Shift + BEAT SYNC, the FLX-4 firmware remaps the
    /// chord to its own note (DDJ-400/FLX-4 map: CUE 0x0C → Shift+CUE 0x48), so the
    /// plain CUE note never arrives while Shift is held.</summary>
    public int ShiftCueNote { get; set; } = 0x48;
    /// <summary>Headphone CUE toggle note, per deck.</summary>
    public int CueNote { get; set; } = 0x54;
    public int MasterCueChannel { get; set; } = 7;
    public int MasterCueNote { get; set; } = 0x63;

    public int LoadChannel { get; set; } = 7;
    public int LoadDeck0Note { get; set; } = 0x46;
    public int LoadDeck1Note { get; set; } = 0x47;

    // --- Notes: pads -------------------------------------------------------------
    // Each deck has a dedicated pad channel; the pad mode the hardware is in decides which note range its
    // eight pads send, a pad's note being <page base> + pad index (0-7). Only the HOT CUE and PAD FX1
    // ranges were captured on this unit; the Shift channels, BEAT JUMP and SAMPLER match Mixxx's DDJ-FLX4
    // values and are not yet captured on this unit.
    public int PadDeck0Channel { get; set; } = 8;
    public int PadDeck1Channel { get; set; } = 10;
    /// <summary>Shift + pad: the firmware sends the same notes on the next pad channel (deck 1 → 9, deck 2
    /// → 11). Matches Mixxx's DDJ-FLX4 values; not yet captured on this unit.</summary>
    public int PadShiftDeck0Channel { get; set; } = 9;
    public int PadShiftDeck1Channel { get; set; } = 11;
    /// <summary>HOT CUE page: note of pad 0 on either deck's pad channel; pad N = PadNoteBase + N.</summary>
    public int PadNoteBase { get; set; } = 0x00;
    /// <summary>BEAT JUMP page: note of pad 0. Matches Mixxx's DDJ-FLX4 values; not yet captured on this
    /// unit.</summary>
    public int PadBeatJumpNoteBase { get; set; } = 0x20;
    /// <summary>SAMPLER page: note of pad 0. Matches Mixxx's DDJ-FLX4 values; not yet captured on this
    /// unit.</summary>
    public int PadSamplerNoteBase { get; set; } = 0x30;
    /// <summary>Beats each BEAT JUMP pad (1-8) moves the playhead by; negative = back.</summary>
    public double[] BeatJumpPadBeats { get; set; } = [-1, 1, -2, 2, -4, 4, -8, 8];
    /// <summary>SAMPLER page: which pad indices (0-based) mute Drums / Vocals / Instrumental
    /// (counted from <see cref="PadSamplerNoteBase"/>).</summary>
    public int StemDrumsPad { get; set; } = 0;
    public int StemVocalsPad { get; set; } = 1;
    public int StemInstrumentalPad { get; set; } = 2;
    /// <summary>SAMPLER page: which pad indices solo Drums / Vocals / Instrumental.</summary>
    public int StemSoloDrumsPad { get; set; } = 4;
    public int StemSoloVocalsPad { get; set; } = 5;
    public int StemSoloInstrumentalPad { get; set; } = 6;
    /// <summary>The Shift pages' pad note bases: note of pad 0 on the KEYBOARD (Shift + HOT CUE), PAD FX2
    /// (Shift + PAD FX1), BEAT LOOP (Shift + BEAT JUMP) and KEY SHIFT (Shift + SAMPLER) pages. Match Mixxx's
    /// DDJ-FLX4 values; not yet captured on this unit.</summary>
    public int PadKeyboardNoteBase { get; set; } = 0x40;
    public int PadFx2NoteBase { get; set; } = 0x50;
    public int PadBeatLoopNoteBase { get; set; } = 0x60;
    public int PadKeyShiftNoteBase { get; set; } = 0x70;
    /// <summary>Beats of the loop each BEAT LOOP pad (1-8) toggles.</summary>
    public double[] BeatLoopPadBeats { get; set; } = [0.25, 0.5, 1, 2, 4, 8, 16, 32];

    // --- Notes: pad pages ----------------------------------------------------------
    // The FLX-4's pad-mode buttons live on each deck's own channel
    // (Deck0Channel/Deck1Channel) and switch the controller's own pad mode.
    // HOT CUE and PAD FX1 were captured via MIDI dump; BEAT JUMP and SAMPLER match Mixxx's DDJ-FLX4
    // values and are not yet captured on this unit.
    public int PadModeHotCueNote { get; set; } = 0x1B;
    public int PadModePadFx1Note { get; set; } = 0x1E;
    public int PadModeBeatJumpNote { get; set; } = 0x20;
    public int PadModeSamplerNote { get; set; } = 0x22;
    /// <summary>Shift + a pad-mode button: the firmware sends its own note for the chord, on the same deck
    /// channel — Shift + HOT CUE = KEYBOARD, Shift + PAD FX1 = PAD FX2, Shift + BEAT JUMP = BEAT LOOP,
    /// Shift + SAMPLER = KEY SHIFT. Match Mixxx's DDJ-FLX4 values; not yet captured on this unit.</summary>
    public int PadModeKeyboardNote { get; set; } = 0x69;
    public int PadModePadFx2Note { get; set; } = 0x6B;
    public int PadModeBeatLoopNote { get; set; } = 0x6D;
    public int PadModeKeyShiftNote { get; set; } = 0x6F;
    /// <summary>PAD FX1 page: note of pad 0; pad N = PadFx1NoteBase + N (captured: pads 1-3 →
    /// 0x10/0x11/0x12). Pad 0 is the echo toggle, pad 1 the roll; the rest are unmapped.</summary>
    public int PadFx1NoteBase { get; set; } = 0x10;

    // --- Notes: beat loop ----------------------------------------------------------
    public int BeatLoopToggleNote { get; set; } = 0x4D;
    public int BeatLoopHalveNote { get; set; } = 0x51;
    public int BeatLoopDoubleNote { get; set; } = 0x53;
    public int BeatLoopToggleBars { get; set; } = 4;

    // --- Notes: beatgrid nudge -----------------------------------------------------
    public int NudgeGridChannel { get; set; } = 5;
    public int NudgeGridBackNote { get; set; } = 0x4A;
    public int NudgeGridForwardNote { get; set; } = 0x4B;

    // --- Notes: beat sync ------------------------------------------------------------
    public int BeatSyncNote { get; set; } = 0x58;
    /// <summary>Shift + BEAT SYNC — the FLX-4 firmware remaps the chord to its own note.</summary>
    public int CycleTempoRangeNote { get; set; } = 0x60;

    // --- CCs: mixer ---------------------------------------------------------------
    public int CrossfaderChannel { get; set; } = 7;
    public int CrossfaderControl { get; set; } = 0x1F;

    public int ChannelVolumeControl { get; set; } = 0x13;
    public int TempoControl { get; set; } = 0x00;

    public int FilterChannel { get; set; } = 7;
    public int FilterDeck0Control { get; set; } = 0x17;
    public int FilterDeck1Control { get; set; } = 0x18;

    public int EqHighControl { get; set; } = 0x07;
    public int EqMidControl { get; set; } = 0x0B;
    public int EqLowControl { get; set; } = 0x0F;

    // --- CCs: browse / jog -----------------------------------------------------------
    public int BrowseRotateChannel { get; set; } = 7;
    public int BrowseRotateControl { get; set; } = 0x40;
    /// <summary>Legacy big browse rotary (mixer section), on some firmwares.</summary>
    public int BrowseRotateLegacyChannel { get; set; } = 11;
    public int BrowseRotateLegacyControl { get; set; } = 0x40;

    public int JogTopPlatterControl { get; set; } = 0x22;
    /// <summary>Top-platter touch sensor, per deck channel: NoteOn while a hand is
    /// on the platter top, NoteOff when it lifts (captured via MIDI dump
    /// 2026-09-05). Lets the App know "hand on" directly instead of guessing
    /// from tick gaps.</summary>
    public int JogTouchNote { get; set; } = 0x36;
    public int JogSideRingControl { get; set; } = 0x21;
    /// <summary>The firmware retransmits top-platter rotation on this CC while the
    /// deck's Shift is held (captured via MIDI dump) — drives the 4× fast search.</summary>
    public int JogTopPlatterShiftControl { get; set; } = 0x29;
    /// <summary>Center (rest) value of the jog CC — delta is measured from here.</summary>
    public int JogCenter { get; set; } = 64;

    // --- Lights -------------------------------------------------------------------
    // The FLX-4 lights a button by echoing its input note back with a velocity,
    // on the button's own NoteOn status byte (0x90 + wire channel 0).
    /// <summary>NoteOn status base; a deck's per-deck light status = this + deck index.</summary>
    public int DeckLightStatusBase { get; set; } = 0x90;
    public int CueLightNote { get; set; } = 0x54;
    public int MasterCueLightStatus { get; set; } = 0x96;
    public int MasterCueLightNote { get; set; } = 0x63;
    public int BeatSyncLightNote { get; set; } = 0x58;
    public byte ButtonLightOnVelocity { get; set; } = 0x7F;
    public byte ButtonLightOffVelocity { get; set; } = 0x00;

    // Pad LEDs: the pad's own note echoed on its pad-channel NoteOn status byte
    // (DeckLightStatusBase + pad channel - 1, i.e. deck0 → 0x97, deck1 → 0x99). The pads are single-colour:
    // lit or dark, the colours being on screen only. These bytes match Mixxx's DDJ-FLX4 values and are not
    // yet captured on this unit.
    public byte PadLightOnVelocity { get; set; } = 0x7F;
    public byte PadLightOffVelocity { get; set; } = 0x00;
}
