namespace Sholto.Data;

/// <summary>Open the beatgrid tuning tool on the deck the grid edit targets.</summary>
public readonly record struct OpenGridEditor(Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
