namespace Sholto.Data;

/// <summary>A deck's beat-synced echo was switched on or off.</summary>
public readonly record struct EchoChanged(int Deck, bool On) : IStateEvent
{
    public int Slot => Deck;
}
