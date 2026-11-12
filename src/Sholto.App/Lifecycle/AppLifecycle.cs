using Sholto.App.Analysis.Harmony;
using Sholto.App.ExternalTools;
using Sholto.App.Storage;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.App.Lifecycle.Folders;

namespace Sholto.App.Lifecycle;

/// <summary>See <see cref="IAppLifecycle"/>. These properties of the startup order are load-bearing and
/// each is kept as it was in the old App.InitializeServices:
/// <list type="bullet">
/// <item>P1 the database opens in a <c>Task.Run</c> once <see cref="Start"/> is called (after the first
/// paint); <see cref="ILibraryDatabase.OpenAsync"/> completes <c>Opened</c> in a <c>finally</c>, with null
/// on failure.</item>
/// <item>P2 the tag, crate and marker services attach only after a successful open.</item>
/// <item>P3 the analysis stores stay no-ops until then (they await <c>Opened</c>; nothing here changes that).</item>
/// <item>P4 BPM, multiplier and key seeding, and multiplier persistence, happen only on success.</item>
/// <item>P5 the theme restore waits for <c>Opened</c>; null means no restore and no persistence. The waveform
/// style restore and the backspin time and distance restores follow the same rule; a backspin choice made
/// before the database opens is held and saved once it does, and wins over the saved value.</item>
/// <item>P6 the folders wait for <c>Opened</c>; with no database nothing is persisted and the scan runs
/// with no stores.</item>
/// <item>P7 audio lists the output devices BEFORE it awaits the database.</item>
/// <item>P8 changing the folders or output device skips the settings write when there is no database
/// (the preferences no-op).</item>
/// <item>P9 the folders question is put before the output device's: the device picker waits until the folders
/// are settled (answered or cancelled), unless a saved device is still there, which starts at once.</item>
/// </list>
/// Everything that touches the library, the bus or the decks hops onto the app thread first; the sequence
/// itself runs on the thread pool, as it did. The theme, waveform style and backspin restore live in
/// <see cref="ISettingsRestorer"/>, the Track List in <see cref="ITrackListPersistence"/>, the music and analysis
/// folders in <see cref="ILibraryFolders"/> and the audio device in <see cref="IOutputDeviceChooser"/>.</summary>
public sealed class AppLifecycle(
    ILibraryDatabase database,
    ILibrarySession library,
    IDeckMarkers markers,
    IDeckHotCues hotCues,
    ISettingsRestorer settings,
    ITrackListPersistence trackListPersistence,
    ILibraryFolders folders,
    IOutputDeviceChooser outputDevice,
    IAudioOutput audioOutput,
    SystemCheck systemCheck,
    IAppThread appThread,
    IEventPublisher publisher) : IAppLifecycle
{
    private readonly ILibraryDatabase _database = database;
    private readonly ILibrarySession _library = library;
    private readonly IDeckMarkers _markers = markers;
    private readonly IDeckHotCues _hotCues = hotCues;
    private readonly ISettingsRestorer _settings = settings;
    private readonly ITrackListPersistence _trackListPersistence = trackListPersistence;
    private readonly ILibraryFolders _folders = folders;
    private readonly IOutputDeviceChooser _outputDevice = outputDevice;
    private readonly IAudioOutput _audioOutput = audioOutput;
    private readonly SystemCheck _systemCheck = systemCheck;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    // Completed once the startup folders question is settled (answered, cancelled, or never needed), so the output
    // device picker is never open at the same time as the Folders panel.
    private readonly TaskCompletionSource _foldersSettled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ILibraryFolders Folders => _folders;

    public void Start()
    {
        // Tell the interfaces what the boot-time tool probe found.
        _appThread.Post(() => _publisher.Publish(_systemCheck.ToReported()));

        // Open the library database; persists analysis across runs. Seeding runs as the open's onOpened
        // work, so Opened completes only after it.
        _ = Task.Run(() => _database.OpenAsync(OnDatabaseOpenedAsync));

        // Restore the theme, waveform style and backspin feel; each restore runs on its own task so it
        // doesn't block the folders resolution below.
        _settings.Start();

        // Restore the Track List; a later change is saved once that is done.
        _trackListPersistence.Start();

        // Settle the music and analysis folders (env override -> saved -> the Folders question), then scan.
        _ = Task.Run(ResolveFoldersAsync);

        // Pick audio output device (ask the user on first run or if the saved device is gone), once the folders
        // question is settled.
        _ = _outputDevice.StartAudioAsync(_foldersSettled.Task);
    }

    public void Stop()
    {
        _audioOutput.Stop();
    }

    // ---- Database ------------------------------------------------------------------------------

    private async Task OnDatabaseOpenedAsync(DatabaseStack database)
    {
        await _appThread.InvokeAsync(() =>
        {
            _library.AttachServices(database.Tags, database.Crates);
            _markers.Attach(database.Markers);
            _hotCues.Attach(database.HotCues);
        });

        var bpms = new Dictionary<string, double>(await database.BasicAnalyses.GetDetectedBpmsAsync());
        var mults = new Dictionary<string, double>(await database.TempoMultipliers.GetAllAsync());

        // Read straight from the database parameter, never through the analysis stack's KeyStore: that store
        // awaits ILibraryDatabase.Opened, which completes only after this callback returns, so going
        // through it would deadlock.
        var keys = new Dictionary<string, Key>();
        foreach (var (path, key) in await database.KeyAnalyses.GetAllAsync())
            if (key.Key is { } k) keys[path] = k;

        await _appThread.InvokeAsync(() =>
        {
            _library.SeedKnownBpms(bpms);
            _library.SeedKnownBpmMultipliers(mults);
            _library.SeedKnownKeys(keys);
            // From here on a BPM multiplier the user chooses is persisted.
            _library.AttachMultiplierStore(database.TempoMultipliers);
        });
    }

    // The folders, then the first scan, then (first launch only) the All Tracks load. A cancelled question or an
    // unreachable folder returns null and a menu rescan never comes through here, so neither loads All Tracks.
    private async Task ResolveFoldersAsync()
    {
        string? musicDir;
        try
        {
            musicDir = await _folders.ResolveAsync();
        }
        finally
        {
            // The question is over (or never asked): the output device picker may ask now. Before the scan, which can be long.
            _foldersSettled.TrySetResult();
        }

        if (musicDir is null) return;  // nothing to scan: the saved path is kept for next launch
        await _library.ScanAsync(musicDir, _database.Stores);
        await _trackListPersistence.LoadAllTracksOnFirstLaunchAsync();
    }

    public void Handle(in ChooseTheme command) => _settings.Handle(command);

    public void Handle(in ChooseWaveformStyle command) => _settings.Handle(command);

    public void Handle(in ChangeOutputDevice command) => _outputDevice.Handle(command);

    public void Handle(in ChooseOutputDevice command) => _outputDevice.Handle(command);
}
