using Sholto.App.Analysis;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.App.ExternalTools;
using Sholto.App.Library;
using Sholto.App.Storage;
using Sholto.App.Lifecycle;
using SoundFlow.Structs;

namespace Sholto.Interface.MainUI;

/// <summary>
/// The plain-.NET leaves of the composition root, built ONCE by
/// <see cref="SholtoStackFactory.Build"/> in <c>Program.BuildAvaloniaApp</c>'s
/// <c>AppBuilder.Configure(() => ...)</c> factory —
/// BEFORE <c>App.Initialize()</c> and <c>App.OnFrameworkInitializationCompleted</c> run.
///
/// <para><b>Why here, and why now.</b> Avalonia's own docs say nothing running inside
/// that factory lambda may touch Avalonia types or expect a <c>SynchronizationContext</c>
/// — it runs ahead of <c>StartWithClassicDesktopLifetime</c>'s own setup. Every leaf
/// bundled here was checked against that rule during the trunk hoist (2026-09-12): none
/// constructs a window, a <c>Dispatcher</c>/<c>DispatcherTimer</c>, or touches Avalonia
/// styles/themes/resources. <see cref="ThemeContext"/> (via the theme catalog) is the one leaf that does NOT
/// belong here — <see cref="Theming.ThemeCatalogFactory"/> resolves bundled themes
/// through Avalonia's <c>AssetLoader</c> (<c>avares://</c>), which needs a live
/// application — so it stays a field on <c>App</c>, built in
/// <c>OnFrameworkInitializationCompleted</c> exactly as before.</para>
///
/// <para><b>What still is NOT here.</b> The DB open (<see cref="ILibraryDatabase"/> is carried here, but opened by the
/// app lifecycle's <c>Start</c>), DB seeding, settings reads, and output-device choice
/// stay in <see cref="Core.Lifecycle.IAppLifecycle"/>, deliberately deferred so the window paints
/// its first frame before Sqlite opens — hoisting them would change startup behaviour,
/// not just where the code lives. The controller/orchestrator/recognizer/bus/bindings
/// cluster, the view model, the audio engine, and the stats timer all need the live
/// window and stay in <c>App</c> too.</para>
/// </summary>
public sealed class SholtoStack(
    ExternalToolStack toolStack,
    FlacDecodeStrategy flacStrategy,
    IAudioFileDecoder decoder,
    IAudioOutputEnumerator outputEnumerator,
    IPipeWireRouter pipeWireRouter,
    ILibraryDatabase libraryDatabase,
    ITrackScanner trackScanner,
    AnalysisStack analysisStack,
    IDeckFactory deckFactory,
    AudioFormat deckFormat,
    ITagRecency tagRecency,
    ICrossfadeCurve crossfade,
    ILibrarySearch librarySearch,
    ProcessStats processStats,
    ISettingPreference themePreference,
    ISettingPreference musicDirPreference,
    ISettingPreference outputDevicePreference)
{
    public ExternalToolStack ToolStack { get; } = toolStack;
    public FlacDecodeStrategy FlacStrategy { get; } = flacStrategy;
    public IAudioFileDecoder Decoder { get; } = decoder;
    public IAudioOutputEnumerator OutputEnumerator { get; } = outputEnumerator;
    public IPipeWireRouter PipeWireRouter { get; } = pipeWireRouter;
    public ILibraryDatabase LibraryDatabase { get; } = libraryDatabase;
    public ITrackScanner TrackScanner { get; } = trackScanner;
    public AnalysisStack AnalysisStack { get; } = analysisStack;
    public IDeckFactory DeckFactory { get; } = deckFactory;
    public AudioFormat DeckFormat { get; } = deckFormat;
    public ITagRecency TagRecency { get; } = tagRecency;
    public ICrossfadeCurve Crossfade { get; } = crossfade;
    public ILibrarySearch LibrarySearch { get; } = librarySearch;
    public ProcessStats ProcessStats { get; } = processStats;
    public ISettingPreference ThemePreference { get; } = themePreference;
    public ISettingPreference MusicDirPreference { get; } = musicDirPreference;
    public ISettingPreference OutputDevicePreference { get; } = outputDevicePreference;
}
