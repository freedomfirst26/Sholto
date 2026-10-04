namespace Sholto.App.Decks;

/// <summary>Builds the session for one deck over a freshly created audio deck.</summary>
public interface IDeckSessionFactory
{
    /// <summary>The session for deck <paramref name="index"/> (0 = deck 1, 1 = deck 2).</summary>
    IDeckSession Create(int index);
}
