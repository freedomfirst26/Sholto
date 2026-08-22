namespace Sholto.Controller;

public enum JogSource
{
    TopPlatter, // capacitive top surface — fast scrub / scratch
    SideRing,   // outer ridged ring — slow / fine seek
}

public abstract record ControllerEvent
{
    public record BrowseRotated(int Delta) : ControllerEvent;
    public record BrowsePressed : ControllerEvent;
    /// <summary>Browse / song-select button released. Paired with <see cref="BrowsePressed"/>
    /// so callers can implement long-press behaviour (e.g. hold to re-analyze).</summary>
    public record BrowseReleased : ControllerEvent;
    public record PlayPressed(int Deck) : ControllerEvent;
    /// <summary>The "LOAD" button for a specific deck (0=Deck 1, 1=Deck 2).</summary>
    public record LoadToDeck(int Deck) : ControllerEvent;
    /// <summary>Crossfader moved. Position is normalized 0..1 (0 = full Deck 1, 1 = full Deck 2).</summary>
    public record CrossfaderMoved(double Position) : ControllerEvent;
    /// <summary>A per-deck channel fader moved. Value 0..1.</summary>
    public record ChannelVolumeMoved(int Deck, double Value) : ControllerEvent;
    /// <summary>Jog wheel rotation. Delta is +/- ticks (1 tick = one click of the wheel).</summary>
    public record JogRotated(int Deck, int Delta, JogSource Source) : ControllerEvent;

    /// <summary>One of the three EQ pots (HI / MID / LOW). Value 0..1, 0.5 = neutral / 0 dB.</summary>
    public record EqMoved(int Deck, EqBand Band, double Value) : ControllerEvent;

    /// <summary>Per-deck COLOR / FILTER knob. Position 0..1; 0.5 = bypass,
    /// &lt; 0.5 = low-pass (further left = lower cutoff), &gt; 0.5 = high-pass
    /// (further right = higher cutoff). Center detent on FLX-4 sends val ≈ 64.</summary>
    public record FilterMoved(int Deck, double Position) : ControllerEvent;

    /// <summary>Hot-cue button press temporarily repurposed as a stem mute toggle.
    /// <paramref name="Group"/>: 0=Drums, 1=Vocals, 2=Instrumental (bass+other).</summary>
    public record StemToggle(int Deck, int Group) : ControllerEvent;

    /// <summary>Tempo (pitch) fader position. <paramref name="Position"/> is 0..1,
    /// 0.5 = neutral. The deck multiplies the offset by the currently selected
    /// pitch range (±6 / ±10 / ±16 / wide).</summary>
    public record TempoMoved(int Deck, double Position) : ControllerEvent;

    /// <summary>FLX-4 4 BEAT / EXIT button. Toggles an N-bar auto-loop on the
    /// deck — first press engages (snapping in to the nearest downbeat),
    /// second press exits. The Pioneer button is labelled "4 BEAT" but in
    /// practice it acts as a 4-bar loop (one musical phrase), which is the
    /// useful scale for DJing.</summary>
    public record BeatLoopToggle(int Deck, int Bars) : ControllerEvent;

    /// <summary>FLX-4 ½× button. Halves the active loop's length (loop-in stays).</summary>
    public record BeatLoopHalve(int Deck) : ControllerEvent;

    /// <summary>FLX-4 2× button. Doubles the active loop's length, clamped to track end.</summary>
    public record BeatLoopDouble(int Deck) : ControllerEvent;

    /// <summary>Nudge the beatgrid (downbeats) by ±N beats. If a loop is active
    /// on the target deck, it moves by the same offset so the listener can tune
    /// by ear. Deck = -1 means "any deck that has an active loop (or deck 0 if
    /// neither)" — the App router picks. Beats: typically ±1.</summary>
    public record NudgeGrid(int Deck, int Beats) : ControllerEvent;

    /// <summary>FLX-4 per-deck Shift modifier press / release. The FLX-4
    /// itself remaps OTHER button presses while Shift is held (e.g. Left
    /// becomes ch=5 0x4A), but those combined events don't say which
    /// deck's Shift was used. We track this state in the App layer to
    /// route combined events (like NudgeGrid) to the correct deck.</summary>
    public record DeckShift(int Deck, bool Pressed) : ControllerEvent;

    /// <summary>Momentary modifier that reinterprets the 3 EQ knobs on
    /// both decks as stem-group attenuators (HI → Drums, MID → Vocals,
    /// LOW → Instrumental). Sent on a dedicated FLX-4 button (ch=5 0x47)
    /// — separate from Shift so the Shift key stays free for the existing
    /// arrow-nudge gesture and any future Shift-modified bindings.</summary>
    public record StemLevelMode(bool Pressed) : ControllerEvent;

    /// <summary>FLX-4 BEAT SYNC button (ch=1/2 0x58), plain press. Beat sync
    /// is not yet implemented, so this is currently a no-op.</summary>
    public record BeatSyncPressed(int Deck) : ControllerEvent;

    /// <summary>CUE button (ch=1/2 0x0C). Repurposed here as "set bar 1 of
    /// the beatgrid at the current playhead" — pause, jog to a kick, press
    /// CUE, and that kick becomes a downbeat. One-button grid phase
    /// alignment, saved per-track.</summary>
    public record SetDownbeatHere(int Deck) : ControllerEvent;

    /// <summary>Shift + BEAT SYNC. The FLX-4 firmware remaps this chord to
    /// its own note (ch=1/2 0x60) with Shift held, so we map it directly —
    /// no need to track Shift state. Cycles the tempo / pitch-fader range
    /// (±6 → ±10 → ±16 → WIDE → ±6) like Rekordbox's TEMPO RANGE.</summary>
    public record CyclePitchRange(int Deck) : ControllerEvent;
}

public enum EqBand { Low, Mid, High }
