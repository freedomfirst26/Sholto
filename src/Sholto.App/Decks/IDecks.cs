namespace Sholto.App.Decks;

/// <summary>The two decks, as headless <see cref="IDeckSession"/>s, never view models. One of the real
/// roles extracted from the old <c>IDeckHost</c>; <see cref="DeckPair"/> implements it. What the performance
/// buckets, cue routing and the command handlers load tracks into, play, and route per-deck gestures to.</summary>
public interface IDecks
{
    IDeckSession Deck1 { get; }
    IDeckSession Deck2 { get; }
    IDeckSession DeckFor(int deck);
}
