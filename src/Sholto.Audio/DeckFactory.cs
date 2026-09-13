using Sholto.Analysis;
using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Default <see cref="IDeckFactory"/>. Composed once in App.axaml.cs, holding
/// the decoder, stem analysis step, reporter, analysis provider
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
/// three DB-backed collaborators below are each a null-object/switchable pair
/// (<see cref="SwitchableAnalysisCache"/>, <see cref="SwitchableKeyAnalysisStore"/>,
/// <see cref="SwitchableGridAdjustmentStore"/>): every Deck gets a real, non-null
/// collaborator immediately, it just degrades to memory-only analysis and no
/// persisted key/grid cache until the composition root calls each switchable's
/// <c>Attach</c> once the DB opens — at which point already-constructed decks
/// pick up the real backing store on their very next call, with nothing on
/// Deck itself ever mutated.
/// </summary>
public sealed class DeckFactory : IDeckFactory
{
    private readonly IAudioFileDecoder _decoder;
    private readonly IStemAnalysisStep _stemAnalyzer;
    private readonly IAnalysisReporter _reporter;
    private readonly IAnalysisProvider _analysisProvider;
    private readonly IKeyAnalysisStore _keyCache;
    private readonly IGridAdjustmentStore _gridCache;
    private readonly IWaveformPeakAnalyzer _peakAnalyzer;
    private readonly IKeyAnalyzer _keyAnalyzer;
    private readonly IVocalRegionAnalyzer _vocalRegionAnalyzer;
    private readonly IBeatgridFitter _beatgridFitter;
    // Ordered post-mix effect chain, handed in rather than defaulted here so
    // the true composition root (App.axaml.cs) decides it — see
    // DeckEffectFactories's doc for why order matters and how a 4th effect
    // is added without touching this factory.
    private readonly IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> _effectFactories;

    public DeckFactory(
        IAudioFileDecoder decoder,
        IStemAnalysisStep stemAnalyzer,
        IAnalysisReporter reporter,
        IAnalysisProvider analysisProvider,
        IKeyAnalysisStore keyCache,
        IGridAdjustmentStore gridCache,
        IWaveformPeakAnalyzer peakAnalyzer,
        IKeyAnalyzer keyAnalyzer,
        IVocalRegionAnalyzer vocalRegionAnalyzer,
        IBeatgridFitter beatgridFitter,
        IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> effectFactories)
    {
        _decoder = decoder;
        _stemAnalyzer = stemAnalyzer;
        _reporter = reporter;
        _analysisProvider = analysisProvider;
        _keyCache = keyCache;
        _gridCache = gridCache;
        _peakAnalyzer = peakAnalyzer;
        _keyAnalyzer = keyAnalyzer;
        _vocalRegionAnalyzer = vocalRegionAnalyzer;
        _beatgridFitter = beatgridFitter;
        _effectFactories = effectFactories;
    }

    public Deck Create() => new(_decoder, _stemAnalyzer, _reporter, _analysisProvider, _keyCache, _gridCache, _peakAnalyzer, _keyAnalyzer, _vocalRegionAnalyzer, _beatgridFitter, _effectFactories);
}
