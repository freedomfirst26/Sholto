using Sholto.App.Analysis.Harmony;
using Sholto.App.Audio;
using Sholto.App.ExternalTools;
using Sholto.App.Storage;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Glance;
using Sholto.App.Library;
using Sholto.App.Library.Crates;
using Sholto.App.Lifecycle.Folders;
using Sholto.App.Performance;
using System.Globalization;
using System.Text.Json;

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
/// itself runs on the thread pool, as it did.</summary>
public sealed class AppLifecycle(
    ILibraryDatabase database,
    ILibrarySession library,
    IDeckMarkers markers,
    ISettingPreference themePreference,
    ILibraryFolders folders,
    ISettingPreference outputDevicePreference,
    ISettingPreference waveformStylePreference,
    ISettingPreference backspinTimePreference,
    ISettingPreference backspinDistancePreference,
    IBackspinFeel backspinFeel,
    ISettingPreference legacyShortlistPreference,
    ISettingPreference trackListPreference,
    ITrackList trackList,
    ISavedTrackListCodec trackListCodec,
    IAudioOutputEnumerator outputEnumerator,
    IAudioOutput audioOutput,
    IControllerSoundCard controllerSoundCard,
    SystemCheck systemCheck,
    IAppThread appThread,
    IEventPublisher publisher) : IAppLifecycle
{
    private readonly ILibraryDatabase _database = database;
    private readonly ILibrarySession _library = library;
    private readonly IDeckMarkers _markers = markers;
    private readonly ISettingPreference _themePreference = themePreference;
    private readonly ILibraryFolders _folders = folders;
    private readonly ISettingPreference _outputDevicePreference = outputDevicePreference;
    private readonly ISettingPreference _waveformStylePreference = waveformStylePreference;
    private readonly ISettingPreference _backspinTimePreference = backspinTimePreference;
    private readonly ISettingPreference _backspinDistancePreference = backspinDistancePreference;
    private readonly IBackspinFeel _backspinFeel = backspinFeel;
    private readonly ISettingPreference _legacyShortlistPreference = legacyShortlistPreference;
    private readonly ISettingPreference _trackListPreference = trackListPreference;
    private readonly ITrackList _trackList = trackList;
    private readonly ISavedTrackListCodec _trackListCodec = trackListCodec;
    private readonly IAudioOutputEnumerator _outputEnumerator = outputEnumerator;
    private readonly IAudioOutput _audioOutput = audioOutput;
    private readonly IControllerSoundCard _controllerSoundCard = controllerSoundCard;
    private readonly SystemCheck _systemCheck = systemCheck;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;

    // The output device question currently put to the user, if any: completed by ChooseOutputDevice.
    private TaskCompletionSource<string?>? _deviceAnswer;
    // Completed once the startup folders question is settled (answered, cancelled, or never needed), so the output
    // device picker is never open at the same time as the Folders panel.
    private readonly TaskCompletionSource _startupQuestions = new(TaskCreationOptions.RunContinuationsAsynchronously);
    // The device master really plays on, from the last route the audio output reported (app thread only). Null until the engine starts.
    private string? _masterDevice;
    // True once the saved theme was restored on a database that opened: from then on a chosen theme is saved.
    private bool _themePersistence;
    // Same rule for the waveform style.
    private bool _waveformStylePersistence;
    // And for the backspin time and distance. A choice made before then is held (not dropped) and saved
    // when persistence arms; guarded by _backspinGate because the choice and the restore are on different threads.
    private readonly object _backspinGate = new();
    private bool _backspinTimePersistence;
    private double? _pendingBackspinTime;
    private bool _backspinDistancePersistence;
    private double? _pendingBackspinDistance;
    // And for the Track List. The restore task is awaited by the first scan, which loads All Tracks
    // when there was no saved list at all (first launch); a saved empty list stays empty.
    private bool _trackListPersistence;
    private bool _trackListFirstLaunch;
    private Task _trackListRestored = Task.CompletedTask;

    public ILibraryFolders Folders => _folders;

    public void Start()
    {
        // Tell the interfaces what the boot-time tool probe found.
        _appThread.Post(() => _publisher.Publish(_systemCheck.ToReported()));

        // Open the library database; persists analysis across runs. Seeding runs as the open's onOpened
        // work, so Opened completes only after it.
        _ = Task.Run(() => _database.OpenAsync(OnDatabaseOpenedAsync));

        // Restore the previously-selected theme. Runs on its own task so it doesn't block the music-dir
        // resolution below.
        _ = Task.Run(RestoreThemeAsync);

        // Restore the previously-selected waveform style, the same way.
        _ = Task.Run(RestoreWaveformStyleAsync);

        // Restore the backspin time and distance, the same way; a choice is saved once that is done.
        _backspinFeel.TimeChosen += OnBackspinTimeChosen;
        _backspinFeel.DistanceChosen += OnBackspinDistanceChosen;
        _ = Task.Run(RestoreBackspinTimeAsync);
        _ = Task.Run(RestoreBackspinDistanceAsync);

        // Restore the Track List, the same way; a later change is saved once that is done.
        _trackList.Changed += OnTrackListChanged;
        _trackListRestored = Task.Run(RestoreTrackListAsync);

        // Settle the music and analysis folders (env override -> saved -> the Folders question), then scan.
        _ = Task.Run(ResolveFoldersAsync);

        // Pick audio output device (ask the user on first run or if the saved device is gone).
        _ = StartAudioAsync();
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

    // ---- Theme ---------------------------------------------------------------------------------

    private async Task RestoreThemeAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var savedName = await _themePreference.GetAsync();
        if (!string.IsNullOrEmpty(savedName))
            await _appThread.InvokeAsync(() => _publisher.Publish(new SavedThemeFound(savedName)));

        _themePersistence = true;
    }

    public void Handle(in ChooseTheme command)
    {
        if (!_themePersistence) return;
        var name = command.Name;
        _ = Task.Run(async () =>
        {
            try
            {
                await _themePreference.SetAsync(name);
            }
            catch (Exception ex) { Console.WriteLine($"[Theme] persist failed: {ex.Message}"); }
        });
    }

    // ---- Waveform style ------------------------------------------------------------------------

    private async Task RestoreWaveformStyleAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var savedId = await _waveformStylePreference.GetAsync();
        if (!string.IsNullOrEmpty(savedId))
            await _appThread.InvokeAsync(() => _publisher.Publish(new SavedWaveformStyleFound(savedId)));

        _waveformStylePersistence = true;
    }

    public void Handle(in ChooseWaveformStyle command)
    {
        if (!_waveformStylePersistence) return;
        var id = command.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                await _waveformStylePreference.SetAsync(id);
            }
            catch (Exception ex) { Console.WriteLine($"[WaveformStyle] persist failed: {ex.Message}"); }
        });
    }

    // ---- Backspin time and distance -------------------------------------------------------------

    private async Task RestoreBackspinTimeAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var saved = await _backspinTimePreference.GetAsync();
        if (double.TryParse(saved, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            await _appThread.InvokeAsync(() =>
            {
                // A choice made while the database was opening wins over the saved value.
                lock (_backspinGate) { if (_pendingBackspinTime is not null) return; }
                _backspinFeel.RestoreSeconds(seconds);
            });

        double? pending;
        lock (_backspinGate)
        {
            _backspinTimePersistence = true;
            pending = _pendingBackspinTime;
            _pendingBackspinTime = null;
        }
        if (pending is { } value) SaveBackspin(_backspinTimePreference, value, "BackspinTime");
    }

    private void OnBackspinTimeChosen(double seconds)
    {
        lock (_backspinGate)
        {
            if (!_backspinTimePersistence)
            {
                _pendingBackspinTime = seconds;
                return;
            }
        }
        SaveBackspin(_backspinTimePreference, seconds, "BackspinTime");
    }

    private async Task RestoreBackspinDistanceAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var saved = await _backspinDistancePreference.GetAsync();
        if (double.TryParse(saved, NumberStyles.Float, CultureInfo.InvariantCulture, out var beats))
            await _appThread.InvokeAsync(() =>
            {
                lock (_backspinGate) { if (_pendingBackspinDistance is not null) return; }
                _backspinFeel.RestoreBeats(beats);
            });

        double? pending;
        lock (_backspinGate)
        {
            _backspinDistancePersistence = true;
            pending = _pendingBackspinDistance;
            _pendingBackspinDistance = null;
        }
        if (pending is { } value) SaveBackspin(_backspinDistancePreference, value, "BackspinDistance");
    }

    private void OnBackspinDistanceChosen(double beats)
    {
        lock (_backspinGate)
        {
            if (!_backspinDistancePersistence)
            {
                _pendingBackspinDistance = beats;
                return;
            }
        }
        SaveBackspin(_backspinDistancePreference, beats, "BackspinDistance");
    }

    private void SaveBackspin(ISettingPreference preference, double value, string tag)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        _ = Task.Run(async () =>
        {
            try
            {
                await preference.SetAsync(text);
            }
            catch (Exception ex) { Console.WriteLine($"[{tag}] persist failed: {ex.Message}"); }
        });
    }

    // ---- Track List -----------------------------------------------------------------------------

    private async Task RestoreTrackListAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var saved = await _trackListPreference.GetAsync();
        if (saved is null)
            _trackListFirstLaunch = true;
        else if (ParseTrackList(saved) is { } list)
        {
            var entries = list.Entries.Select(e => new TrackListEntry(e.Path, e.SourceKeys)).ToList();
            var sources = list.Sources.Select(s => new TrackListSource(s.Key, s.Kind, s.Name, 0)).ToList();
            await _appThread.InvokeAsync(() => _trackList.Restore(entries, sources));
        }

        _trackListPersistence = true;
        await MigrateShortlistAsync();
    }

    // One time: the retired shortlist's songs join the end of the Track List as songs (de-duplicated by the
    // list), then the saved shortlist is emptied so no later startup adds them again.
    private async Task MigrateShortlistAsync()
    {
        var paths = ParseLegacyShortlist(await _legacyShortlistPreference.GetAsync());
        if (paths.Count == 0) return;

        await _appThread.InvokeAsync(() =>
        {
            foreach (var path in paths)
                _trackList.Handle(new LoadSongToTrackList(path, _migrationOrigin));
        });
        await _legacyShortlistPreference.SetAsync("[]");
    }

    private readonly Origin _migrationOrigin = new(InterfaceIds.MainUI, "startup", "shortlist-migration");

    private IReadOnlyList<string> ParseLegacyShortlist(string? saved)
    {
        if (string.IsNullOrEmpty(saved)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(saved) ?? [];
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[Shortlist] saved list unreadable, not migrated: {ex.Message}");
            return [];
        }
    }

    private SavedTrackList? ParseTrackList(string saved)
    {
        try
        {
            return _trackListCodec.Decode(saved);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[TrackList] saved list unreadable, starting empty: {ex.Message}");
            return null;
        }
    }

    // Raised on the app thread: snapshot the list now, write on the pool.
    private void OnTrackListChanged()
    {
        if (!_trackListPersistence) return;
        var json = _trackListCodec.Encode(new SavedTrackList(
            _trackList.Entries.Select(e => new SavedTrackListEntry(e.Path, [.. e.SourceKeys])).ToList(),
            _trackList.Sources.Select(s => new SavedTrackListSource(s.Key, s.Kind, s.Name)).ToList()));
        _ = Task.Run(async () =>
        {
            try
            {
                await _trackListPreference.SetAsync(json);
            }
            catch (Exception ex) { Console.WriteLine($"[TrackList] persist failed: {ex.Message}"); }
        });
    }

    // First launch: once the first scan is done and the new songs are filed, load the "All Tracks" crate.
    private async Task LoadAllTracksOnFirstLaunchAsync()
    {
        await _trackListRestored;
        var crates = _library.Crates;
        if (!_trackListFirstLaunch || crates is null) return;
        _trackListFirstLaunch = false;
        try
        {
            var crateId = await crates.CreateAsync(CrateNames.AllTracks);
            // The scan files songs into the crate in the background: wait until it holds the whole catalog.
            var catalogIds = await _appThread.InvokeAsync(() => _library.Catalog.Select(s => s.TrackId).Where(id => id != Guid.Empty).ToHashSet());
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var members = await crates.TrackIdsAsync(crateId);
                if (catalogIds.IsSubsetOf(members)) break;
                await Task.Delay(100);
            }
            await _appThread.InvokeAsync(() => _trackList.Handle(new LoadCrateToTrackList(crateId, CrateNames.AllTracks, _firstLaunchOrigin)));
        }
        catch (Exception ex) { Console.WriteLine($"[TrackList] first-launch load failed: {ex.Message}"); }
    }

    private readonly Origin _firstLaunchOrigin = new(InterfaceIds.MainUI, "startup", "first-launch");

    // ---- Folders -------------------------------------------------------------------------------

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
            _startupQuestions.TrySetResult();
        }

        if (musicDir is null) return;  // nothing to scan: cancelled or unreachable, the saved path is kept for next launch
        await _library.ScanAsync(musicDir, _database.Stores);
        await LoadAllTracksOnFirstLaunchAsync();
    }

    // ---- Output device -------------------------------------------------------------------------

    private async Task StartAudioAsync()
    {
        // P7: the devices are listed before the database is awaited.
        var allDevices = await Task.Run(() => _outputEnumerator.EnumerateOutputs()).ConfigureAwait(false);
        if (allDevices.Count == 0)
        {
            Console.WriteLine("No audio output devices found.");
            return;
        }

        // The controller's sound card is never a pickable "master speaker" — AudioEngine auto-selects it as
        // the device to open (4ch: master 1-2 + headphone cue 3-4) whenever it's connected, and the picker
        // only offers where to re-route master to. See AudioEngine.Start for the full story.
        bool controllerCardPresent = allDevices.Any(d => _controllerSoundCard.Matches(d.Name));
        var devices = allDevices.Where(d => !_controllerSoundCard.Matches(d.Name)).ToList();

        var savedName = await _outputDevicePreference.GetAsync().ConfigureAwait(false);
        var chosen = devices.FirstOrDefault(d => d.Name == savedName);

        if (chosen is null && devices.Count > 0)
        {
            // Not while the Folders panel is open: wait for its question to be settled.
            await _startupQuestions.Task.ConfigureAwait(false);
            chosen = await RequestDeviceAsync(devices, savedName).ConfigureAwait(false);
        }

        // No speaker chosen (cancelled/none): fine if the controller is here to fall back on (master+cue
        // both play from it, like before this feature). Otherwise there's genuinely nothing to play through.
        if (chosen is null && !controllerCardPresent) return;

        if (chosen is not null)
            await _outputDevicePreference.SetAsync(chosen.Name).ConfigureAwait(false);

        var route = await _audioOutput.StartAsync(chosen?.Name).ConfigureAwait(false);
        await _appThread.InvokeAsync(() => _masterDevice = route.DeviceName).ConfigureAwait(false);
    }

    /// <summary>Menu entry point: ask which device master should play through and switch to it. Nothing
    /// happens if the user cancels or picks the device already in use. If the controller's card is the only
    /// device connected there is nothing to offer; master stays on it.</summary>
    public void Handle(in ChangeOutputDevice command) => _ = ChangeOutputDeviceAsync();

    private async Task ChangeOutputDeviceAsync()
    {
        if (_deviceAnswer is not null) return;  // already asking

        // Excludes the controller's sound card — see StartAudioAsync's comment.
        var allDevices = await Task.Run(() => _outputEnumerator.EnumerateOutputs()).ConfigureAwait(false);
        var devices = allDevices.Where(d => !_controllerSoundCard.Matches(d.Name)).ToList();
        if (devices.Count == 0) return;

        // Where master really plays (not the saved wish), if it is one of the devices on offer. A failed route
        // leaves it on the controller, which is not in the list: then nothing reads as in use and the speakers
        // can be picked again.
        var masterDevice = await _appThread.InvokeAsync(() => _masterDevice).ConfigureAwait(false);
        var currentName = devices.Any(d => d.Name == masterDevice) ? masterDevice : null;
        var chosen = await RequestDeviceAsync(devices, currentName).ConfigureAwait(false);
        if (chosen is null || chosen.Name == currentName) return;

        await _outputDevicePreference.SetAsync(chosen.Name).ConfigureAwait(false);

        var route = await _audioOutput.SwitchAsync(chosen.Name).ConfigureAwait(false);
        await _appThread.InvokeAsync(() => _masterDevice = route.DeviceName).ConfigureAwait(false);
    }

    public void Handle(in ChooseOutputDevice command)
    {
        var answer = _deviceAnswer;
        if (answer is null) return;
        _deviceAnswer = null;
        answer.TrySetResult(command.Name);
    }

    private async Task<AudioDevice?> RequestDeviceAsync(IReadOnlyList<AudioDevice> devices, string? currentName)
    {
        var choices = devices.Select(d => new OutputDeviceChoice(d.Name, d.IsDefault)).ToList();
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _appThread.InvokeAsync(() =>
        {
            _deviceAnswer = answer;
            _publisher.Publish(new OutputDeviceNeeded(choices, currentName));
        });
        var name = await answer.Task.ConfigureAwait(false);
        return name is null ? null : devices.FirstOrDefault(d => d.Name == name);
    }
}
