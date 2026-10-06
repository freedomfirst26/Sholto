using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.App.Storage;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Glance;
using Sholto.App.Lifecycle;
using Sholto.App.Performance;

namespace Sholto.App.Tests;

/// <summary>The production <see cref="AppLifecycle"/> over a <see cref="LibrarySessionRig"/>'s library, a fake
/// database (with every port faked in memory, or unavailable), settings preferences over it, fake output
/// devices and a recording audio output. Every question the App asks is recorded on
/// <see cref="MusicFolderAsked"/> / <see cref="DeviceAsked"/>; a test answers by sending the command. The
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
        AudioDevice[]? devices = null,
        SystemCheck? systemCheck = null)
    {
        // One real thread for everything that touches the bus, as the UI thread is in the app.
        var appThread = new SingleThreadAppThread();
        _appThread = appThread;
        Library = new LibrarySessionRig(appThread);
        Tags = new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>());
        Stack = databaseAvailable
            ? new DatabaseStack(
                Tags, Library.Crates, Markers, Library.Catalog, Settings, Library.Multipliers,
                new FakeBasicAnalysisStore(new Dictionary<string, double>()), new NullKeyAnalysisStore(),
                new FakeGridAdjustmentStore())
            : null;
        Database = new FakeLibraryDatabase(Stack, gated);
        ThemePreference = new SettingPreference(Database, SettingsKeys.Theme);
        MusicDirPreference = new SettingPreference(Database, SettingsKeys.MusicDir);
        OutputDevicePreference = new SettingPreference(Database, SettingsKeys.OutputDevice);
        WaveformStylePreference = new SettingPreference(Database, SettingsKeys.WaveformStyle);
        BackspinTimePreference = new SettingPreference(Database, SettingsKeys.BackspinTimeSeconds);
        BackspinDistancePreference = new SettingPreference(Database, SettingsKeys.BackspinDistanceBeats);
        ShortlistPreference = new SettingPreference(Database, SettingsKeys.GlanceShortlist);
        Shortlist = new Shortlist(Library.Library, Library.Bus);
        Library.Bus.Subscribe(BackspinTimeAnnounced);
        Library.Bus.Subscribe(BackspinDistanceAnnounced);
        BackspinFeel = new BackspinFeel(Library.Bus);
        Enumerator = new FakeAudioOutputEnumerator(devices ?? []);
        DeckMarkers = new DeckMarkers(new DeckPair(Library.Deck1, Library.Deck2), Library.Library, appThread, Library.Bus);
        Lifecycle = new AppLifecycle(
            Database, Library.Library, DeckMarkers, ThemePreference, MusicDirPreference, OutputDevicePreference,
            WaveformStylePreference, BackspinTimePreference, BackspinDistancePreference, BackspinFeel, ShortlistPreference, Shortlist,
            Enumerator, Audio, new FakeControllerSoundCard(ControllerCard), systemCheck ?? new SystemCheck([]), appThread, Library.Bus, musicDirOverride);
        Library.Bus.Subscribe(MusicFolderAsked);
        Library.Bus.Subscribe(DeviceAsked);
        Library.Bus.Subscribe(ThemeFound);
        Library.Bus.Subscribe(WaveformStyleFound);
    }

    public const string ControllerCard = "DDJ-FLX4 Analog";

    public LibrarySessionRig Library { get; }
    public FakeTagService Tags { get; }
    public FakeMarkerService Markers { get; } = new();
    public FakeSettingsStore Settings { get; } = new();
    public DatabaseStack? Stack { get; }
    public FakeLibraryDatabase Database { get; }
    public ISettingPreference ThemePreference { get; }
    public ISettingPreference MusicDirPreference { get; }
    public ISettingPreference OutputDevicePreference { get; }
    public ISettingPreference WaveformStylePreference { get; }
    public ISettingPreference BackspinTimePreference { get; }
    public ISettingPreference BackspinDistancePreference { get; }
    public ISettingPreference ShortlistPreference { get; }
    public Shortlist Shortlist { get; }
    public BackspinFeel BackspinFeel { get; }
    public FakeAudioOutputEnumerator Enumerator { get; }
    public RecordingAudioOutput Audio { get; } = new();
    public DeckMarkers DeckMarkers { get; }
    public AppLifecycle Lifecycle { get; }

    public RecordingHandler<MusicFolderNeeded> MusicFolderAsked { get; } = new();
    public RecordingHandler<OutputDeviceNeeded> DeviceAsked { get; } = new();
    public RecordingHandler<SavedThemeFound> ThemeFound { get; } = new();
    public RecordingHandler<SavedWaveformStyleFound> WaveformStyleFound { get; } = new();
    public RecordingHandler<BackspinTimeChanged> BackspinTimeAnnounced { get; } = new();
    public RecordingHandler<BackspinDistanceChanged> BackspinDistanceAnnounced { get; } = new();
}
