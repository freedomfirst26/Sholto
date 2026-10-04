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
        var decoder = new NoOpAudioFileDecoder();
        var stemStep = new NoOpStemAnalysisStep();
        var analysisProvider = new AnalysisProvider(
            compute: (_, _) => throw new NotSupportedException(
                "Sholto.Interface.Bench renders via Deck.LoadStreaming, which never reaches AnalysisProvider."));
        var deckFactory = new BenchDeckFactory(decoder, stemStep, analysisProvider, Options.Create(new WaveformBandOptions()));
        var benchDeck = new BenchDeck(deckFactory);
        var crossfade = new EqualPowerCrossfade();
        var deckAdvance = new DeckAdvance(deckFactory);

        // The one bus, as in the app: the deck sessions publish, the controller adapter subscribes,
        // and the command handlers register. Everything runs on the caller's thread.
        var bus = new DataBus(new BenchHandlerFailureSink());
        var appThread = new ImmediateAppThread();
        var coreFactory = new HeadlessCoreFactory(benchDeck, deckFactory, decoder, bus, new ManualFrameClock(), appThread);
        var stackFactory = new HeadlessInputStackFactory(
            bus, bus, bus, bus, bus,
            new CommandHandlersFactory(appThread, bus),
            new PerformanceFactory(),
            new ControllerInputFactory(),
            Options.Create(new ScratchOptions()),
            Options.Create(new MagnetismOptions()),
            new MasterCueOutput(),
            new NullAppLifecycle());
        var headlessHost = new HeadlessHost(
            coreFactory,
            new GestureHostFactory(Options.Create(new ControllerMappingsOptions())),
            stackFactory,
            appThread,
            deckAdvance,
            new ScenarioGestureBuilder(),
            new CoreSnapshot(),
            new StateDiff(),
            crossfade);

        return new BenchCli(
            new ScenarioParser(),
            new OfflineRenderer(benchDeck, crossfade, new CueOutputRouterFactory()),
            new FfmpegSoundMeter(),
            benchDeck,
            deckAdvance,
            headlessHost,
            crossfade,
            new InputLatencyBenchmark(headlessHost)).Run(args);
    }
}
