namespace Sholto.App.Decks;

/// <summary>See <see cref="IDecks"/>: the two sessions the composition root built.</summary>
public sealed class DeckPair(IDeckSession deck1, IDeckSession deck2) : IDecks
{
    public IDeckSession Deck1 { get; } = deck1;
    public IDeckSession Deck2 { get; } = deck2;

    public IDeckSession DeckFor(int deck) => deck == 1 ? Deck2 : Deck1;
}
