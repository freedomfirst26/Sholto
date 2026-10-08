using Sholto.App;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Decks;
using Sholto.App.Dsp;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.Bench.Rendering;

namespace Sholto.Interface.Bench.Headless;

/// <summary>
/// Builds the headless core the way the app's composition root does, but over the same no-op
/// decode/segment/stem fakes as <see cref="BenchDeck"/> and with no DB, no audio device and no
/// MIDI controller behind it. The two decks still need <see cref="Deck.AttachEngine"/> — normally done
/// deep inside <c>AudioEngine</c>'s constructor when it opens a real device — so this calls it
/// directly against a Bench offline engine (same trick as <see cref="IBenchDeck.Create"/>): a real
/// port call, not a new seam. Shared by the headless host and by the MainUI harness, which puts a
/// real window over the same core.
/// </summary>
/// <param name="benchDeck">Builds the offline engine the decks attach to.</param>
/// <param name="deckFactory">The factory the deck sessions' decks are built through.</param>
/// <param name="decoder">The decoder handed to the core's track loader (the bench's no-op one, or a real one).</param>
/// <param name="publisher">The data bus the deck sessions publish their state on.</param>
/// <param name="clock">The frame clock the deck sessions time the end-of-track flash from.</param>
/// <param name="appThread">The app thread the deck sessions marshal analysis events onto.</param>
public sealed class HeadlessCoreFactory(IBenchDeck benchDeck, IBenchDeckFactory deckFactory, IAudioFileDecoder decoder,
    IEventPublisher publisher, IFrameClock clock, IAppThread appThread) : IHeadlessCoreFactory
{
    private readonly IBenchDeck _benchDeck = benchDeck;
    private readonly IBenchDeckFactory _deckFactory = deckFactory;
    private readonly IAudioFileDecoder _decoder = decoder;
    private readonly IEventPublisher _publisher = publisher;
    private readonly IFrameClock _clock = clock;
    private readonly IAppThread _appThread = appThread;

    public HeadlessCore Build()
    {
        var engine = _benchDeck.CreateEngine();
        // A filesystem query against a directory that never exists always answers "not present".
        var stemPresence = new DemucsStemPresence(_ => Path.Combine(Path.GetTempPath(), "sholto-bench-no-stems"));

        var capturingFactory = new CapturingDeckFactory(_deckFactory);
        var sectionOptions = new PhraseSectionOptions();
        var sectionAnalyzer = new PhraseSectionAnalyzer(
            new BarFeatureExtractor(sectionOptions), new PhraseSectionLabeler(sectionOptions), sectionOptions);
        var sessions = new DeckSessionFactory(capturingFactory, sectionAnalyzer, _clock, _appThread, _publisher);
        var deck1 = sessions.Create(0);
        var deck2 = sessions.Create(1);
        var reporter = new AnalysisReporter(new[] { AnalysisSteps.Beats });
        var core = new CoreFactory(
            new TrackScanner(), new NullKeyAnalysisStore(), stemPresence,
            reporter, new KeyAnalyzer(), _decoder,
            new EqualPowerCrossfade(), _appThread, _publisher, _clock,
            new GatedStemSeparator(new NoOpStemAnalysisStep(), new SemaphoreStemGate(), reporter),
            Microsoft.Extensions.Options.Options.Create(new LoadOptions())).Build(deck1, deck2);

        var decks = capturingFactory.Created.ToArray();
        decks[0].AttachEngine(engine, _deckFactory.DeckFormat);
        decks[1].AttachEngine(engine, _deckFactory.DeckFormat);
        return new HeadlessCore(core, decks[0], decks[1], engine);
    }
}
