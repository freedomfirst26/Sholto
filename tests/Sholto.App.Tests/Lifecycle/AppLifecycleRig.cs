using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.App.Storage;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Lifecycle;

namespace Sholto.App.Tests;

/// <summary>The production <see cref="AppLifecycle"/> over a <see cref="LibrarySessionRig"/>'s library, a fake
/// database (with every port faked in memory, or unavailable), settings preferences over it, fake output
/// devices and a recording audio output. Every question the App asks is recorded on
/// <see cref="MusicFolderAsked"/> / <see cref="DeviceAsked"/>; a test answers by sending the command. The
/// app thread is one dedicated thread, as in the app.</summary>
internal sealed class AppLifecycleRig
{
    public static readonly Origin Origin = new(InterfaceIds.Bench, "test", "answer");

    public AppLifecycleRig(
        bool databaseAvailable = true,
        bool gated = false,
        string? musicDirOverride = null,
        AudioDevice[]? devices = null)
    {
        // One real thread for everything that touches the bus, as the UI thread is in the app.
        var appThread = new SingleThreadAppThread();
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
        Enumerator = new FakeAudioOutputEnumerator(devices ?? []);
        DeckMarkers = new DeckMarkers(new DeckPair(Library.Deck1, Library.Deck2), Library.Library, appThread, Library.Bus);
        Lifecycle = new AppLifecycle(
            Database, Library.Library, DeckMarkers, ThemePreference, MusicDirPreference, OutputDevicePreference,
            Enumerator, Audio, new FakeControllerSoundCard(ControllerCard), appThread, Library.Bus, musicDirOverride);
        Library.Bus.Subscribe(MusicFolderAsked);
        Library.Bus.Subscribe(DeviceAsked);
        Library.Bus.Subscribe(ThemeFound);
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
    public FakeAudioOutputEnumerator Enumerator { get; }
    public RecordingAudioOutput Audio { get; } = new();
    public DeckMarkers DeckMarkers { get; }
    public AppLifecycle Lifecycle { get; }

    public RecordingHandler<MusicFolderNeeded> MusicFolderAsked { get; } = new();
    public RecordingHandler<OutputDeviceNeeded> DeviceAsked { get; } = new();
    public RecordingHandler<SavedThemeFound> ThemeFound { get; } = new();
}
