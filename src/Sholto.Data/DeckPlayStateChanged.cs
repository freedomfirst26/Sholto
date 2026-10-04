namespace Sholto.Data;

/// <summary>A deck's transport phase, plus the end-of-track flash phase. While <see cref="Phase"/> is
/// <see cref="PlayPhase.Ending"/> the App republishes this on every flash tick with
/// <see cref="EndFlashOn"/> toggled, so an interface that blinks something (the controller's play
/// light) stays in lockstep with the on-screen disc ring. <see cref="EndFlashOn"/> is false whenever
/// the phase is not Ending.</summary>
public readonly record struct DeckPlayStateChanged(int Deck, PlayPhase Phase, bool EndFlashOn) : IStateEvent
{
    public int Slot => Deck;
}
