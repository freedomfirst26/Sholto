using Sholto.App;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.App.Lifecycle;
using Sholto.App.Storage;
using Sholto.App.Performance;
using Sholto.Data;
using Sholto.Host;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Bench.Rendering;
using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.Controller;
using Sholto.Interface.Keyboard;
using Sholto.Interface.MainUI.Harness.Ui;
using Sholto.Interface.MainUI.Harness.Showcase;
using Sholto.App.Analysis.Harmony;
using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.Harness;

/// <summary>
/// Entry point and composition root of the MainUI harness. <c>Main</c> is the one static (forced by
/// the runtime); it builds each collaborator exactly once, hands them to <see cref="HarnessCli"/>
/// and runs it. The headless core underneath is the same one the bench builds
/// (<see cref="HeadlessCoreFactory"/>); the window, MainUI's input stack and Avalonia.Headless are
/// what this project adds.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var options = new SholtoOptions();
        var decoder = new NoOpAudioFileDecoder();
        var stemStep = new NoOpStemAnalysisStep();
        var analysisProvider = new AnalysisProvider(
            compute: (_, _) => throw new NotSupportedException(
                "The MainUI harness renders via Deck.LoadStreaming, which never reaches AnalysisProvider."));
        var appThread = new AvaloniaAppThread();
        var deckFactory = new BenchDeckFactory(decoder, stemStep, analysisProvider, options.WaveformBands, appThread);
        var benchDeck = new BenchDeck(deckFactory);
        var crossfade = new EqualPowerCrossfade();
        var deckAdvance = new DeckAdvance(deckFactory);

        // Avalonia allows its headless setup once per process: this is the ONE
        // BenchHeadlessApp, handed to the factory the UI host uses. A second
        // instance would throw (see BenchHeadlessApp's doc).
        var headlessApp = new BenchHeadlessApp();
        // The one bus, as in the app: the deck sessions publish, the controller adapter subscribes,
        // the view models follow, and the command handlers register.
        var bus = new DataBus(new ConsoleHandlerFailureSink());
        // The track loader (key 1 / 2, LOAD) decodes for real, so a library track loads onto a deck the way
        // it does in the app. MP3 and WAV only: no ffmpeg, no FLAC engine. The deck's own decode port stays
        // the no-op one above.
        var naudioDecoding = new NAudioDecoding();
        var loaderDecoder = new AudioFileDecoder([new Mp3DecodeStrategy(naudioDecoding), new WavDecodeStrategy(naudioDecoding)]);
        var coreFactory = new HeadlessCoreFactory(benchDeck, deckFactory, loaderDecoder, bus, new ManualFrameClock(), appThread);
        // Disposed when Main returns: deletes the temp library database the scans filled.
        using var demoLibrary = new DemoLibrary(new SholtoStorage(new DatabaseStackFactory(new KeyFactory()), new SholtoDbContextOptionsFactory()));
        var uiHost = new UiHost(
            new BenchAppFactory(headlessApp, coreFactory, bus, bus, bus, options.Feature, appThread),
            deckAdvance,
            new GestureHostFactory(options.Controllers),
            new ScenarioGestureFactory(),
            new CoreSnapshot(),
            new StateDiff(),
            crossfade,
            new InputStackFactory(bus, bus, bus, bus, bus, new CommandHandlersFactory(appThread, bus, new NullHintCounter(), options.Glance), new PerformanceFactory(),
                new ControllerInputFactory(), new KeyboardInputFactory()),
            options,
            new ImmediateAppThread(),
            new MasterCueOutput(),
            new NullAppLifecycle(),
            new ShowcaseAnalysisFactory(new DemoWaveformFactory()),
            new KeyFactory(),
            bus,
            demoLibrary);

        return new HarnessCli(new ScenarioFactory(), uiHost).Run(args);
    }
}
