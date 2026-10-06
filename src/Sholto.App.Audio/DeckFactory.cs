using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stages;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.Data;
using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio;

/// <summary>
/// Default <see cref="IDeckFactory"/>. Composed once in App.axaml.cs, holding
/// the decoder, stem analysis stage, reporter, analysis provider
/// and the two persistence-cache ports shared by both decks. Every
/// <see cref="Create"/> call returns a Deck with all of it already wired —
/// never a Deck a caller has to come back and finish, because every one of
/// these is now a <see cref="Deck"/> CONSTRUCTOR parameter: <c>new Deck(...)</c>
/// with any of them missing simply does not compile.
///
/// The DB — and the real, SQLite-backed <c>AnalysisProvider</c> tier,
/// <c>SqliteKeyAnalysisStore</c> and <c>SqliteGridAdjustmentStore</c> built from it —
/// does not exist yet when this factory builds both decks: the window paints
/// its first frame before App.axaml.cs's InitializeServices opens it. Rather
/// than closing over nullable fields (the previous, half-fixed shape), the
/// three DB-backed collaborators below are each a deferred store
/// (<see cref="DeferredBasicAnalysisStore"/>, <see cref="DeferredKeyAnalysisStore"/>,
/// <see cref="DeferredGridAdjustmentStore"/>): every Deck gets a real, non-null
/// collaborator immediately; each store awaits the open task and then forwards
/// (a DB that failed to open means no persistence), so already-constructed decks need no attach step and nothing on
/// Deck itself is ever mutated.
/// </summary>
public sealed class DeckFactory(
    IAudioFileDecoder decoder,
    IStemStage stemStage,
    IAnalysisReporter reporter,
    IAnalysisProvider analysisProvider,
    IKeyAnalysisStore keyCache,
    IGridAdjustmentStore gridCache,
    IKeyAnalyzer keyAnalyzer,
    IBeatgridFactory beatgrids,
    IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> effectFactories,
    IPlaybackProviderFactory playbackProviders,
    IAppThread appThread) : IDeckFactory
{
    private readonly IAudioFileDecoder _decoder = decoder;
    private readonly IStemStage _stemStage = stemStage;
    private readonly IAnalysisReporter _reporter = reporter;
    private readonly IAnalysisProvider _analysisProvider = analysisProvider;
    private readonly IKeyAnalysisStore _keyCache = keyCache;
    private readonly IGridAdjustmentStore _gridCache = gridCache;
    private readonly IKeyAnalyzer _keyAnalyzer = keyAnalyzer;
    private readonly IBeatgridFactory _beatgrids = beatgrids;
    // Ordered post-mix effect chain, handed in rather than defaulted here so
    // the true composition root (App.axaml.cs) decides it — see
    // DeckEffectFactory's doc for why order matters and how a 4th effect
    // is added without touching this factory.
    private readonly IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> _effectFactories = effectFactories;
    private readonly IPlaybackProviderFactory _playbackProviders = playbackProviders;
    private readonly IAppThread _appThread = appThread;

    public IDeckPorts Create() => new DeckPorts(new Deck(_decoder, _stemStage, _reporter, _analysisProvider, _keyCache, _gridCache, _keyAnalyzer, _beatgrids, _effectFactories, _playbackProviders, _appThread));
}
