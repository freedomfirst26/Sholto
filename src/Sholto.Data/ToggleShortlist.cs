namespace Sholto.Data;

/// <summary>Add a track to the shortlist if absent, remove it if present. The App persists the list.</summary>
/// <param name="FilePath">The track's file path.</param>
/// <param name="Origin">Who sent it.</param>
public readonly record struct ToggleShortlist(string FilePath, Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
