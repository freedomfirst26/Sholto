using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.App.Storage;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Glance;
using Sholto.App.Library;
using Sholto.App.Lifecycle;
using Sholto.App.Lifecycle.Folders;
using Sholto.App.Performance;

namespace Sholto.App.Tests;

/// <summary>The production <see cref="AppLifecycle"/> over a <see cref="LibrarySessionRig"/>'s library, a fake
/// database (with every port faked in memory, or unavailable), settings preferences over it, fake output
/// devices and a recording audio output. Every question the App asks is recorded on
/// <see cref="FoldersAsked"/> / <see cref="DeviceAsked"/>; a test answers by sending the command. The
/// app thread is one dedicated thread, as in the app.</summary>
internal sealed class AppLifecycleRig
{
    public static readonly Origin Origin = new(InterfaceIds.Bench, "test", "answer");

    private readonly SingleThreadAppThread _appThread;

    /// <summary>Run <paramref name="action"/> on the app thread and wait for it, then for the work it posted
    /// to the app thread to drain: domain state is only changed there, as in the app.</summary>
    public async Task OnAppThreadAsync(Action action)
    {
        await RunOnAppThreadAsync(action);
        await RunOnAppThreadAsync(() => { });
    }

    private Task RunOnAppThreadAsync(Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _appThread.Post(() =>
        {
            try { action(); done.SetResult(); }
            catch (Exception e) { done.SetException(e); }
        });
        return done.Task;
    }

    public AppLifecycleRig(
        bool databaseAvailable = true,
        bool gated = false,
        string? musicDirOverride = null,
        string? analysisDirOverride = null,
        AudioDevice[]? devices = null,
        SystemCheck? systemCheck = null)
    {
        // A rig given a music override is not about the folders question, so the analysis folder is fixed too
        // (a temp folder) unless a test gives one: otherwise a fresh install would ask for it.
        analysisDirOverride ??= musicDirOverride is null ? null : Directory.CreateTempSubdirectory("sholto-analysis-").FullName;
        // One real thread for everything that touches the bus, as the UI thread is in the app.
        var appThread = new SingleThreadAppThread();
        _appThread = appThread;
        Library = new LibrarySessionRig(appThread);
        Tags = new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>());
        Stack = databaseAvailable
            ? new DatabaseStack(
                Tags, Library.Crates, Markers, new FakeHotCueStore(), Library.Catalog, Settings, Library.Multipliers,
                new FakeBasicAnalysisStore(new Dictionary<string, double>()), new NullKeyAnalysisStore(),
                new FakeGridAdjustmentStore())
            : null;
        Database = new FakeLibraryDatabase(Stack, gated);
        ThemePreference = new SettingPreference(Database, SettingsKeys.Theme);
        MusicDirPreference = new SettingPreference(Database, SettingsKeys.MusicDir);
        AnalysisDirPreference = new SettingPreference(Database, SettingsKeys.AnalysisDir);
        OutputDevicePreference = new SettingPreference(Database, SettingsKeys.OutputDevice);
        WaveformStylePreference = new SettingPreference(Database, SettingsKeys.WaveformStyle);
        BackspinTimePreference = new SettingPreference(Database, SettingsKeys.BackspinTimeSeconds);
        BackspinDistancePreference = new SettingPreference(Database, SettingsKeys.BackspinDistanceBeats);
        LegacyShortlistPreference = new SettingPreference(Database, SettingsKeys.GlanceShortlist);
        TrackListPreference = new SettingPreference(Database, SettingsKeys.TrackList);
        TrackList = new TrackList(Library.Library, appThread, Library.Bus, Logs.Create("TrackList"));
        Library.Bus.Subscribe(BackspinTimeAnnounced);
        Library.Bus.Subscribe(BackspinDistanceAnnounced);
        BackspinFeel = new BackspinFeel(Library.Bus);
        Enumerator = new FakeAudioOutputEnumerator(devices ?? []);
        DeckMarkers = new DeckMarkers(new DeckPair(Library.Deck1, Library.Deck2), Library.Library, appThread, Library.Bus);
        var folderOptions = Options.Create(new AnalysisFolderOptions());
        Folders = new LibraryFolders(
            MusicDirPreference, AnalysisDirPreference, AnalysisFolder, new SemaphoreStemGate(),
            new StemFolderMover(new DirectoryRename(), folderOptions, Logs.Create("StemMove")), new FolderCheck(folderOptions),
            Library.Library, Database, appThread, Library.Bus, musicDirOverride, analysisDirOverride,
            Directory.CreateTempSubdirectory("sholto-legacy-").FullName, Logs.Create("Library"), Logs.Create("Folders"));
        Lifecycle = new AppLifecycle(
            Database, Library.Library, DeckMarkers, new NullDeckHotCues(),
            new SettingsRestorer(
                Database, ThemePreference, WaveformStylePreference, BackspinTimePreference, BackspinDistancePreference,
                BackspinFeel, appThread, Library.Bus, Logs),
            new TrackListPersistence(
                Database, Library.Library, TrackListPreference, LegacyShortlistPreference, TrackList, Codec, appThread,
                Logs.Create("TrackList"), Logs.Create("Shortlist")),
            Folders,
            new OutputDeviceChooser(
                Enumerator, Audio, new FakeControllerSoundCard(ControllerCard), OutputDevicePreference, appThread, Library.Bus, Logs.Create("")),
            Audio, systemCheck ?? new SystemCheck([]), appThread, Library.Bus);
        Library.Bus.Subscribe(FoldersAsked);
        Library.Bus.Subscribe(DeviceAsked);
        Library.Bus.Subscribe(ThemeFound);
        Library.Bus.Subscribe(WaveformStyleFound);
    }

    public const string ControllerCard = "DDJ-FLX4 Analog";

    public LibrarySessionRig Library { get; }
    /// <summary>Every line the lifecycle's parts wrote.</summary>
    public RecordingLogFactory Logs { get; } = new();
    public FakeTagService Tags { get; }
    public FakeMarkerService Markers { get; } = new();
    public FakeSettingsStore Settings { get; } = new();
    public DatabaseStack? Stack { get; }
    public FakeLibraryDatabase Database { get; }
    public ISettingPreference ThemePreference { get; }
    public ISettingPreference MusicDirPreference { get; }
    public ISettingPreference AnalysisDirPreference { get; }
    public ISettingPreference OutputDevicePreference { get; }
    public ISettingPreference WaveformStylePreference { get; }
    public ISettingPreference BackspinTimePreference { get; }
    public ISettingPreference BackspinDistancePreference { get; }
    public ISettingPreference LegacyShortlistPreference { get; }
    public ISettingPreference TrackListPreference { get; }
    public TrackList TrackList { get; }
    public SavedTrackListCodec Codec { get; } = new();
    public BackspinFeel BackspinFeel { get; }
    public FakeAudioOutputEnumerator Enumerator { get; }
    public RecordingAudioOutput Audio { get; } = new();
    public DeckMarkers DeckMarkers { get; }
    public AnalysisFolder AnalysisFolder { get; } = new();
    public LibraryFolders Folders { get; }
    public AppLifecycle Lifecycle { get; }

    public RecordingHandler<FoldersNeeded> FoldersAsked { get; } = new();
    public RecordingHandler<OutputDeviceNeeded> DeviceAsked { get; } = new();
    public RecordingHandler<SavedThemeFound> ThemeFound { get; } = new();
    public RecordingHandler<SavedWaveformStyleFound> WaveformStyleFound { get; } = new();
    public RecordingHandler<BackspinTimeChanged> BackspinTimeAnnounced { get; } = new();
    public RecordingHandler<BackspinDistanceChanged> BackspinDistanceAnnounced { get; } = new();
}
