using Microsoft.Extensions.Options;
using Sholto.App;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.App.Lifecycle;
using Sholto.App.Performance;
using Sholto.Data;
using Sholto.Interface.Bench.Behaviour;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Bench.Rendering;
using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.Bench.Sound;
using Sholto.Interface.Controller;
using Sholto.Interface.Controller.Mappings;

namespace Sholto.Interface.Bench;

/// <summary>
/// Entry point and composition root of the headless interface. <c>Main</c> is the one static (forced
/// by the runtime); it builds each collaborator exactly once, hands them to <see cref="BenchCli"/> and
/// runs it. All command logic lives in <see cref="BenchCli"/>. No UI toolkit and no MainUI: the graph is
/// the headless core over an immediate app thread and a manual frame clock.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // Real MP3/WAV decode (resampled to 48 kHz): Deck.LoadStreaming falls back to it for files whose
        // rate differs from the engine's, which the streaming provider cannot resample. No ffmpeg/FLAC.
        var naudioDecoding = new NAudioDecoding();
        var decoder = new AudioFileDecoder([new Mp3DecodeStrategy(naudioDecoding), new WavDecodeStrategy(naudioDecoding)]);
        var stemStep = new NoOpStemAnalysisStep();
        var analysisProvider = new AnalysisProvider(
            compute: (_, _) => throw new NotSupportedException(
                "Sholto.Interface.Bench renders via Deck.LoadStreaming, which never reaches AnalysisProvider."));
        var appThread = new ImmediateAppThread();
        var deckFactory = new BenchDeckFactory(decoder, stemStep, analysisProvider, Options.Create(new WaveformBandOptions()), appThread);
        var benchDeck = new BenchDeck(deckFactory);
        var crossfade = new EqualPowerCrossfade();
        var deckAdvance = new DeckAdvance(deckFactory);

        var headlessHost = new HeadlessHostFactory(benchDeck, deckFactory, decoder, deckAdvance, appThread, crossfade).Create();

        return new BenchCli(
            new ScenarioFactory(),
            new OfflineRenderer(benchDeck, crossfade, new CueOutputRouterFactory()),
            new FfmpegSoundMeter(),
            benchDeck,
            deckAdvance,
            headlessHost,
            crossfade,
            new InputLatencyBenchmark(headlessHost)).Run(args);
    }
}
