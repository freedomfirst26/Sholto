using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Loading;
using Sholto.App.Mixer;

namespace Sholto.App;

/// <summary>Holds the leaves the core shares and builds one <see cref="CoreStack"/> per call.</summary>
public sealed class CoreFactory(
    ITrackScanner trackScanner,
    IKeyAnalysisStore keyStore,
    IStemPresence stemPresence,
    IAnalysisReporter reporter,
    IKeyAnalyzer keyAnalyzer,
    IAudioFileDecoder decoder,
    ICrossfadeCurve crossfade,
    IAppThread appThread,
    IEventPublisher publisher) : ICoreFactory
{
    private readonly ITrackScanner _trackScanner = trackScanner;
    // The switchable store: a no-op until the database attaches.
    private readonly IKeyAnalysisStore _keyStore = keyStore;
    private readonly IStemPresence _stemPresence = stemPresence;
    private readonly IAnalysisReporter _reporter = reporter;
    private readonly IKeyAnalyzer _keyAnalyzer = keyAnalyzer;
    private readonly IAudioFileDecoder _decoder = decoder;
    private readonly ICrossfadeCurve _crossfade = crossfade;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;

    public CoreStack Build(IDeckSession deck1, IDeckSession deck2)
    {
        var decks = new DeckPair(deck1, deck2);
        var library = new LibrarySession(
            _trackScanner, _keyStore, _stemPresence, _reporter, decks, new Session(), _appThread, _publisher);
        var markers = new DeckMarkers(decks, library, _appThread, _publisher);
        var loader = new TrackLoader(library, decks, _decoder, _keyStore, _keyAnalyzer, _reporter, _appThread, _publisher);
        return new CoreStack(decks, new PlaybackRequests(decks), new MixerSession(decks, _crossfade, _publisher), library, markers, loader);
    }
}
