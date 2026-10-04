namespace Sholto.Data;

/// <summary>Show only the tracks carrying <see cref="Tag"/>.</summary>
public readonly record struct FilterLibraryByTag(string Tag, Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
