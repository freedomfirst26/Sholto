using Sholto.Analysis;
using Sholto.Audio;

namespace Sholto.Bench.Rendering;

/// <summary>
/// Bench's <see cref="IDeckFactory"/>: the same no-op collaborators
/// <see cref="BenchDeck"/> always used, just built through the port instead
/// of a hand-rolled <c>new Deck(...)</c> — so Bench substitutes a factory
/// rather than duplicating Deck-construction knowledge that now lives in
/// <see cref="DeckFactory"/>.
///
/// Deck's analysis-provider/reporter/cache hooks are now constructor
/// parameters (no half-built Deck is possible any more — see
/// <see cref="DeckFactory"/>'s doc comment), so Bench must supply real,
/// never-null objects here too. It still deliberately does NOT wire them up
/// to anything real: Bench always loads via <c>Deck.LoadStreaming</c>, whose
/// background analysis kick-off decodes through <see cref="NoOpAudioFileDecoder"/>
/// first and always throws there (caught and logged, not thrown further — see
/// <see cref="DeckFakes"/>'s doc), so <see cref="Deck.AnalysisProvider"/> and
/// the caches are never actually reached. Analysis was never part of what
/// this harness renders or measures, so an empty/no-op provider and the
/// shared null-object caches are all it needs.
/// </summary>
public sealed class BenchDeckFactory : IDeckFactory
{
    public static readonly BenchDeckFactory Instance = new();
    private BenchDeckFactory() { }

    private static readonly AnalysisProvider EmptyAnalysisProvider = new(
        caches: [],
        compute: (_, _, _, _) => throw new NotSupportedException(
            "Sholto.Bench renders via Deck.LoadStreaming, which never reaches AnalysisProvider."));

    // Real instances, same reasoning as WaveformPeakAnalyzer just below: these
    // are pure compute (no subprocess/DB), so Bench doesn't need a fake, just
    // an object to satisfy the now constructor-injected ports (pass 3v,
    // 2026-09-12) — Deck.LoadStreaming's background analysis kick-off never
    // reaches them either (see class doc above), so what's passed here never
    // actually runs.
    public Deck Create() => new(
        NoOpAudioFileDecoder.Instance,
        NoOpStemAnalyzer.Instance,
        reporter: new AnalysisReporter(Array.Empty<string>()),
        analysisProvider: EmptyAnalysisProvider,
        keyCache: NullKeyAnalysisStore.Instance,
        gridCache: NullGridAdjustmentStore.Instance,
        peakAnalyzer: new WaveformPeakAnalyzer(),
        keyAnalyzer: new KeyAnalyzer(new CamelotKeys()),
        vocalRegionAnalyzer: new VocalRegionAnalyzer(),
        beatgridFitter: new BeatgridFitter(),
        effectFactories: DeckEffectFactories.Default);
}
