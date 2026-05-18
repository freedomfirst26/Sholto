namespace OpenDJ.Controller;

public abstract record ControllerEvent
{
    public record BrowseRotated(int Delta) : ControllerEvent;
    public record BrowsePressed : ControllerEvent;
    public record PlayPressed(int Deck) : ControllerEvent;
}
