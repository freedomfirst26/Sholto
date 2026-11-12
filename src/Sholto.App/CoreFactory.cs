using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Audio.BatchAnalysis;
using Sholto.App.Dsp;
using Sholto.App.Glance;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Loading;
using Sholto.App.Mixer;
using Sholto.App.Performance;

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
    IEventPublisher publisher,
    IFrameClock frameClock,
    IStemSeparator stemSeparator,
    IOptions<LoadOptions> loadOptions,
    IOptions<HotCueOptions> hotCueOptions,
    ILogFactory logs) : ICoreFactory
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
    private readonly IFrameClock _frameClock = frameClock;
    private readonly IStemSeparator _stemSeparator = stemSeparator;
    private readonly IOptions<LoadOptions> _loadOptions = loadOptions;
    private readonly IOptions<HotCueOptions> _hotCueOptions = hotCueOptions;
    private readonly ILogFactory _logs = logs;
    // Defaults for now; moves into SholtoOptions once that file is free to change.
    private readonly IOptions<BatchAnalysisOptions> _batchOptions = Options.Create(new BatchAnalysisOptions());

    public CoreStack Build(IDeckSession deck1, IDeckSession deck2)
    {
        var decks = new DeckPair(deck1, deck2);
        var catalog = new LibraryCatalog(_publisher);
        var seeds = new LibrarySeeds(catalog);
        var wiring = new LibraryDeckWiring(catalog, decks, new Session(), _reporter, _appThread);
        var library = new LibrarySession(
            _trackScanner, _keyStore, _stemPresence, decks, catalog, wiring, seeds, _appThread, _publisher);
        var markers = new DeckMarkers(decks, library, _appThread, _publisher);
        var hotCues = new DeckHotCues(decks, library, _appThread, _publisher, markers, _hotCueOptions);
        var searchPick = new SearchPick(library);
        var loader = new TrackLoader(library, decks, _decoder, _keyStore, _keyAnalyzer, _reporter, _appThread, _publisher, searchPick,
            new LoadGuard(_frameClock, _publisher, _loadOptions), new LoadUndo(_frameClock, _publisher, _loadOptions), _stemSeparator);
        var libraryAnalysis = new LibraryAnalysisHandler(catalog,
            new LibraryBatchAnalyzerFactory(_decoder, _keyStore, _keyAnalyzer, decks, library, _batchOptions, _logs), _appThread, _publisher);
        return new CoreStack(decks, new PlaybackRequests(decks, hotCues), new MixerSession(decks, _crossfade, _publisher), library, markers, hotCues, loader,
            new BackspinFeel(_publisher), searchPick, new TrackList(library, _appThread, _publisher), libraryAnalysis);
    }
}
