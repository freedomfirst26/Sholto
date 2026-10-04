namespace Sholto.Data;

/// <summary>Show the whole library again.</summary>
public readonly record struct ClearLibraryFilter(Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
