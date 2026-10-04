using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Audio;
using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>Holds the collaborators every <see cref="DeckSession"/> shares and builds one per call, over a
/// new deck from <see cref="IDeckFactory"/>.</summary>
public sealed class DeckSessionFactory(
    IDeckFactory decks,
    ISongSegmentAnalyzer songSegmentAnalyzer,
    IFrameClock clock,
    IAppThread appThread,
    IEventPublisher publisher) : IDeckSessionFactory
{
    private readonly IDeckFactory _decks = decks;
    private readonly ISongSegmentAnalyzer _songSegmentAnalyzer = songSegmentAnalyzer;
    private readonly IFrameClock _clock = clock;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;

    public IDeckSession Create(int index)
    {
        var session = new DeckSession(index, _decks.Create(), _songSegmentAnalyzer, _clock, _appThread, _publisher);
        // The session publishes the transport phase, stem mutes and echo itself; this publishes the rest of
        // what an interface shows, from the first moment, so a subscriber that joins later is replayed it.
        new DeckEventPublisher(session, _publisher).Start();
        return session;
    }
}
