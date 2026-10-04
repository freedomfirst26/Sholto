namespace Sholto.Data;

/// <summary>The marker positions (seconds) on a deck's loaded track. Shared by reference; do not modify. State.</summary>
public readonly record struct DeckMarkersChanged(int Deck, double[] Markers) : IStateEvent
{
    public int Slot => Deck;
}
