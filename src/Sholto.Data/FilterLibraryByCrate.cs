namespace Sholto.Data;

/// <summary>Show only the tracks in a crate.</summary>
public readonly record struct FilterLibraryByCrate(int CrateId, string Name, Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
