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
}

public enum EqBand { Low, Mid, High }
