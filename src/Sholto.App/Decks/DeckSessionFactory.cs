using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Audio;
using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>Holds the collaborators every <see cref="DeckSession"/> shares and builds one per call, over a
/// new deck from <see cref="IDeckFactory"/>.</summary>
public sealed class DeckSessionFactory(
    IDeckFactory decks,
    IPhraseSectionAnalyzer sectionAnalyzer,
    IFrameClock clock,
    IAppThread appThread,
    IEventPublisher publisher) : IDeckSessionFactory
{
    private readonly IDeckFactory _decks = decks;
    private readonly ISectionLayoutFactory _sectionLayouts = new SectionLayoutFactory(sectionAnalyzer);
    private readonly IFrameClock _clock = clock;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;

    public IDeckSession Create(int index)
    {
        var ports = _decks.Create();
        var session = new DeckSession(index, ports, new DeckGain(ports.Mixer), new DeckStemMix(index, ports.Stems, _publisher), new DeckTuneEditor(ports.Beatgrid), new DeckTempoControl(ports.Loading, ports.Tempo), new DeckPlayPhase(index, _clock, _publisher), _sectionLayouts, _appThread, _publisher);
        // The session publishes the transport phase, stem mutes and echo itself; this publishes the rest of
        // what an interface shows, from the first moment, so a subscriber that joins later is replayed it.
        new DeckEventPublisher(session, _publisher).Start();
        return session;
    }
}
