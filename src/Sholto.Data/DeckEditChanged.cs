namespace Sholto.Data;

/// <summary>A deck's editing state: the tune editor is open, the two-point grid edit is armed, the grid has been nudged. State.</summary>
public readonly record struct DeckEditChanged(int Deck, bool EditOpen, bool GridEditActive, bool IsGridNudged) : IStateEvent
{
    public int Slot => Deck;
}
