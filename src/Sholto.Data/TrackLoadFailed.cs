namespace Sholto.Data;

/// <summary>A track could not be loaded into a deck (a toast-worthy fact); the deck stays usable.</summary>
public readonly record struct TrackLoadFailed(int Deck, string FilePath, string Title) : IEvent;
