namespace Sholto.Data;

/// <summary>What a deck holds: the loaded track, how far its load has got, and the analysis derived from it.
/// Published when a track is loading, loaded, unloaded, or an analysis step lands.
/// State: a late subscriber is told the current content.
/// <para>An immutable snapshot, republished on every analysis step: a subscriber keeps the latest and never
/// sees one change underneath it.</para></summary>
/// <param name="IsLoaded">The deck's audio engine holds a track. During a load this is still true for the previous track until the new samples land.</param>
public readonly record struct DeckContentChanged(
    int Deck, DeckTrack? Track, DeckLoadState LoadState, bool IsLoaded, DeckAnalysis? Analysis) : IStateEvent
{
    public int Slot => Deck;
}
