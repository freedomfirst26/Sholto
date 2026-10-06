using Sholto.Data;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Stems;
using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stages;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stores;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Analysis.Processing;
using Sholto.App.Audio;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Sholto.Interface.Bench.Rendering;

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
/// <see cref="NoOpAudioFileDecoder"/>'s doc), so <see cref="Deck.AnalysisProvider"/> and
/// the caches are never actually reached. Analysis was never part of what
/// this harness renders or measures, so an empty/no-op provider and the
/// shared null-object caches are all it needs.
/// </summary>
/// <param name="decoder">The no-op decoder every deck is built with.</param>
/// <param name="stemStep">The no-op stem analysis step inside every deck's StemAnalysisStage.</param>
/// <param name="analysisProvider">The empty analysis provider every deck is built with.</param>
/// <param name="waveformBands">The waveform band options the peak analyzer is built with.</param>
public sealed class BenchDeckFactory(IAudioFileDecoder decoder, IStemAnalysisStep stemStep, IAnalysisProvider analysisProvider,
    IOptions<WaveformBandOptions> waveformBands, IAppThread appThread) : IBenchDeckFactory
{
    private readonly BeatgridFactory _beatgrids = new();
    private readonly PlaybackProviderFactory _playbackProviders = new();

    // The same effect chain the real app composes (SholtoStackFactory), built here
    // so Bench renders through identical EQ -> filter -> echo -> beat-repeat.
    private readonly DeckEffectFactory _effects = new(new TempoMath());

    /// <summary>The format every Bench deck is attached with: 48 kHz stereo F32,
    /// the same deck format <see cref="AudioEngine"/> uses.</summary>
    public AudioFormat DeckFormat { get; } = new()
    {
        SampleRate = AudioFileDecoder.TargetSampleRate,
        Channels = 2,
        Format = SampleFormat.F32,
    };

    // Real instances, same reasoning as WaveformPeakAnalyzer just below: these
    // are pure compute (no subprocess/DB), so Bench doesn't need a fake, just
    // an object to satisfy the now constructor-injected ports (pass 3v,
    // 2026-09-12) — Deck.LoadStreaming's background analysis kick-off never
    // reaches them either (see class doc above), so what's passed here never
    // actually runs.
    public IDeckPorts Create() => new DeckPorts(CreateDeck());

    /// <summary>The concrete Deck, for Bench (a composition root) which needs internals and AttachEngine.</summary>
    public Deck CreateDeck() => new(
        decoder,
        // stemStep.IsAvailable is false (NoOpStemAnalysisStep), the same path a
        // demucs-less machine takes. Never reached anyway: Deck.LoadStreaming's
        // background analysis kick-off decodes through NoOpAudioFileDecoder first,
        // which always throws (see class doc above).
        new StemAnalysisStage(
            stemStep,
            new StemDecoder(decoder),
            new WaveformPeakAnalyzer(new WaveformBandSplitterFactory(new BiquadFactory(), waveformBands), new WaveformPeaksFactory()),
            new VocalRegionAnalyzer(),
            new AnalysisReporter(Array.Empty<string>()),
            new FixedStemDevice(StemDevice.Cpu),
            new SemaphoreStemGate()),
        reporter: new AnalysisReporter(Array.Empty<string>()),
        analysisProvider: analysisProvider,
        keyCache: new NullKeyAnalysisStore(),
        gridCache: new NullGridAdjustmentStore(),
        keyAnalyzer: new KeyAnalyzer(),
        beatgrids: _beatgrids,
        effectFactories: _effects.Chain,
        playbackProviders: _playbackProviders,
        appThread: appThread);
}
