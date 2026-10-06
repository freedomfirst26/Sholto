namespace Sholto.Data;

/// <summary>Undo the last accepted load: within 10 s, restore the deck's previous track (or empty it) at the
/// previous position, paused.</summary>
public readonly record struct UndoLastLoad(Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
