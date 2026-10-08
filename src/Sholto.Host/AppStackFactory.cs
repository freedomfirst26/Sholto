using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Stems;
using Sholto.Data;
using Sholto.App.Audio;
using Sholto.App.ExternalTools;
using Sholto.App.Storage;
using Sholto.Interface.Controller;
using Sholto.Interface.Keyboard;
using Sholto.App;
using Sholto.App.Lifecycle;
using Sholto.App.Performance;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.Views;

using Sholto.Interface.MainUI;

namespace Sholto.Host;

public sealed class AppStackFactory : IAppStackFactory
{
    public AppStack Create()
    {
        // One KeyFactory, shared by the analysis stack and the storage's
        // database factory (which builds the key serializer).
        var keyFactory = new KeyFactory();
        var options = new SholtoOptions();
        // The app thread (the UI thread), shared by the decks, the entities and the input stack.
        var appThread = new AvaloniaAppThread();
        var stackFactory = new SholtoStackFactory(
            new ExternalToolStackFactory(new ExternalToolFinderFactory(), new MadmomBeatAnalysisStepFactory(), options.StemSeparation),
            new AnalysisStackFactory(keyFactory, options.WaveformBands),
            new SholtoStorage(new DatabaseStackFactory(keyFactory), new SholtoDbContextOptionsFactory()),
            appThread);
        var stack = stackFactory.Build();
        // The one bus: the App side publishes onto it, the interfaces subscribe to it.
        var bus = new DataBus(new ConsoleHandlerFailureSink());
        // Its frame clock, shared by the entities and the input stack.
        var frameClock = new DispatcherFrameClock();
        // MASTER CUE reaches the audio engine through this once the engine exists.
        var masterCueOutput = new MasterCueOutput();
        var coreFactory = new CoreFactory(
            stack.TrackScanner, stack.AnalysisStack.KeyStore, stack.ToolStack.StemCache, stack.AnalysisStack.Reporter,
            stack.AnalysisStack.KeyAnalyzer, stack.Decoder, stack.Crossfade, appThread, bus, frameClock,
            new GatedStemSeparator(stack.ToolStack.Stems, stack.StemGate, stack.AnalysisStack.Reporter), options.Load);
        var lifecycleFactory = new AppLifecycleFactory(
            stack.LibraryDatabase, stack.ThemePreference, stack.MusicDirPreference, stack.OutputDevicePreference,
            stack.WaveformStylePreference, stack.BackspinTimePreference, stack.BackspinDistancePreference, stack.LegacyShortlistPreference,
            stack.TrackListPreference, new SavedTrackListCodec(),
            stack.OutputEnumerator, new AudioEngineFactory(new CueOutputRouterFactory()), [stack.FlacStrategy],
            stack.PipeWireRouter, stack.DeckFormat, masterCueOutput, stack.ToolStack.Check, appThread, bus,
            Environment.GetEnvironmentVariable("SHOLTO_MUSIC_DIR"));
        var iconFactory = new AppIconFactory();
        return new AppStack(new LiveEntities(bus, bus, bus, bus, frameClock, appThread, iconFactory), stack, options,
            coreFactory, lifecycleFactory, new LifecyclePromptsFactory(bus, bus, appThread),
            new ThemeStackFactory(), new ControllerStackFactory(),
            new InputStackFactory(bus, bus, bus, bus, bus, new CommandHandlersFactory(appThread, bus, new HintCounter(stack.LibraryDatabase), options.Glance), new PerformanceFactory(),
                new ControllerInputFactory(), new KeyboardInputFactory()),
            masterCueOutput, frameClock, appThread, bus, new TrayFactory(iconFactory));
    }
}
