namespace Sholto.Data;

/// <summary>A deck's loop: whether one is engaged and where, in seconds (both 0 when not). State.</summary>
public readonly record struct DeckLoopChanged(int Deck, bool Active, double StartSec, double EndSec) : IStateEvent
{
    public int Slot => Deck;
}
