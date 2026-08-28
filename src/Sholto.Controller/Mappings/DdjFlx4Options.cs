namespace Sholto.Controller.Mappings;

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

    public int StemLevelModeChannel { get; set; } = 5;
    public int StemLevelModeNote { get; set; } = 0x47;

    // --- Notes: transport --------------------------------------------------------
    public int PlayNote { get; set; } = 0x0B;
    /// <summary>Headphone CUE toggle note, per deck.</summary>
    public int CueNote { get; set; } = 0x54;
    public int MasterCueChannel { get; set; } = 7;
    public int MasterCueNote { get; set; } = 0x63;

    public int LoadChannel { get; set; } = 7;
    public int LoadDeck0Note { get; set; } = 0x46;
    public int LoadDeck1Note { get; set; } = 0x47;

    // --- Notes: pads -------------------------------------------------------------
    // Hot Cue pad mode emits notes 0x00-0x07 on each deck's dedicated pad
    // channel — sequential, so a pad's note is PadNoteBase + pad index (0-7).
    public int PadDeck0Channel { get; set; } = 8;
    public int PadDeck1Channel { get; set; } = 10;
    /// <summary>Note of pad 0 on either deck's pad channel; pad N = PadNoteBase + N.</summary>
    public int PadNoteBase { get; set; } = 0x00;
    /// <summary>Which pad indices (0-based) mute Drums / Vocals / Instrumental.</summary>
    public int StemDrumsPad { get; set; } = 0;
    public int StemVocalsPad { get; set; } = 1;
    public int StemInstrumentalPad { get; set; } = 2;

    // --- Notes: pad pages ----------------------------------------------------------
    // The FLX-4's HOT CUE / PAD FX1 buttons live on each deck's own channel
    // (Deck0Channel/Deck1Channel) and switch the controller's own pad mode —
    // captured via MIDI dump. Pressing one changes which note range the pads
    // below emit (still on PadDeck0Channel/PadDeck1Channel).
    public int PadModeHotCueNote { get; set; } = 0x1B;
    public int PadModePadFx1Note { get; set; } = 0x1E;
    /// <summary>Note of pad 0 in PAD FX1 mode; pad N = PadFx1NoteBase + N (captured:
    /// pads 1-3 → 0x10/0x11/0x12). Only pad 0 (the echo toggle) is mapped for now.</summary>
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
    public int CyclePitchRangeNote { get; set; } = 0x60;

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

    /// <summary>
    /// Pad LED on/off velocity, echoed on the pad's own NoteOn status byte
    /// (DeckLightStatusBase + pad channel - 1, i.e. deck0 → 0x97, deck1 → 0x99).
    /// UNVERIFIED ON HARDWARE — copied from the Cue/BeatSync "echo the note"
    /// convention by assumption; needs a real controller check before trusting it.
    /// </summary>
    public byte PadLightOnVelocity { get; set; } = 0x7F;
    public byte PadLightOffVelocity { get; set; } = 0x00;
}
