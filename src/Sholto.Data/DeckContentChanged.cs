namespace Sholto.Data;

/// <summary>What a deck holds: the loaded track, how far its load has got, and the analysis and sections
/// derived from it. Published when a track is loading, loaded, unloaded, or an analysis step lands.
/// State: a late subscriber is told the current content.
/// <para>Generic over the content types because Sholto.Data knows no App types: the root closes it over
/// the App's track, analysis and segment types, and the App and the interface agree on that one closed
/// type. The analysis is shared by reference and fills in as steps land; each such step publishes again.</para></summary>
/// <param name="IsLoaded">The deck's audio engine holds a track. During a load this is still true for the previous track until the new samples land.</param>
/// <typeparam name="TTrack">The App's track type.</typeparam>
/// <typeparam name="TAnalysis">The App's per-track analysis bag.</typeparam>
/// <typeparam name="TSegment">One structural section of the track.</typeparam>
public readonly record struct DeckContentChanged<TTrack, TAnalysis, TSegment>(
    int Deck, TTrack? Track, DeckLoadState LoadState, bool IsLoaded, TAnalysis? Analysis,
    IReadOnlyList<TSegment>? Segments) : IStateEvent
{
    public int Slot => Deck;
}
