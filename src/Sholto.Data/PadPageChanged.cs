namespace Sholto.Data;

/// <summary>A deck's active pad page changed (or was re-selected). Interfaces light their pad-mode
/// buttons from this and force a device that holds its own pad mode into the same page.</summary>
public readonly record struct PadPageChanged(int Deck, PadPage Page) : IStateEvent
{
    public int Slot => Deck;
}
