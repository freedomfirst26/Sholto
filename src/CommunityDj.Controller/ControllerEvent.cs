namespace CommunityDj.Controller;

public enum JogSource
{
    TopPlatter, // capacitive top surface — fast scrub / scratch
    SideRing,   // outer ridged ring — slow / fine seek
}

public abstract record ControllerEvent
{
    public record BrowseRotated(int Delta) : ControllerEvent;
    public record BrowsePressed : ControllerEvent;
    public record PlayPressed(int Deck) : ControllerEvent;
    /// <summary>The "LOAD" button for a specific deck (0=Deck 1, 1=Deck 2).</summary>
    public record LoadToDeck(int Deck) : ControllerEvent;
    /// <summary>Crossfader moved. Position is normalized 0..1 (0 = full Deck 1, 1 = full Deck 2).</summary>
    public record CrossfaderMoved(double Position) : ControllerEvent;
    /// <summary>A per-deck channel fader moved. Value 0..1.</summary>
    public record ChannelVolumeMoved(int Deck, double Value) : ControllerEvent;
    /// <summary>Jog wheel rotation. Delta is +/- ticks (1 tick = one click of the wheel).</summary>
    public record JogRotated(int Deck, int Delta, JogSource Source) : ControllerEvent;
}
