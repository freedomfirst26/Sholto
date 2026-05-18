namespace OpenDJ.Controller;

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
    /// <summary>Jog wheel rotation. Delta is +/- ticks (1 tick = one click of the wheel).</summary>
    public record JogRotated(int Deck, int Delta, JogSource Source) : ControllerEvent;
}
