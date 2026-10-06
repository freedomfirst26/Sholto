using Sholto.App.Analysis.Harmony;
using Sholto.App.Audio;
using Sholto.App.ExternalTools;
using Sholto.App.Storage;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Glance;
using Sholto.App.Library;
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
/// <item>P6 the music folder waits for <c>Opened</c>; with no database nothing is persisted and the scan runs
/// with no stores.</item>
/// <item>P7 audio lists the output devices BEFORE it awaits the database.</item>
/// <item>P8 changing the music folder or output device skips the settings write when there is no database
/// (the preferences no-op).</item>
/// </list>
/// Everything that touches the library, the bus or the decks hops onto the app thread first; the sequence
/// itself runs on the thread pool, as it did.</summary>
public sealed class AppLifecycle(
    ILibraryDatabase database,
    ILibrarySession library,
    IDeckMarkers markers,
    ISettingPreference themePreference,
    ISettingPreference musicDirPreference,
    ISettingPreference outputDevicePreference,
    ISettingPreference waveformStylePreference,
    ISettingPreference backspinTimePreference,
    ISettingPreference backspinDistancePreference,
    IBackspinFeel backspinFeel,
    ISettingPreference shortlistPreference,
    IShortlist shortlist,
    IAudioOutputEnumerator outputEnumerator,
    IAudioOutput audioOutput,
    IControllerSoundCard controllerSoundCard,
    SystemCheck systemCheck,
    IAppThread appThread,
    IEventPublisher publisher,
    string? musicDirOverride) : IAppLifecycle
{
    private readonly ILibraryDatabase _database = database;
    private readonly ILibrarySession _library = library;
    private readonly IDeckMarkers _markers = markers;
    private readonly ISettingPreference _themePreference = themePreference;
    private readonly ISettingPreference _musicDirPreference = musicDirPreference;
    private readonly ISettingPreference _outputDevicePreference = outputDevicePreference;
    private readonly ISettingPreference _waveformStylePreference = waveformStylePreference;
    private readonly ISettingPreference _backspinTimePreference = backspinTimePreference;
    private readonly ISettingPreference _backspinDistancePreference = backspinDistancePreference;
    private readonly IBackspinFeel _backspinFeel = backspinFeel;
    private readonly ISettingPreference _shortlistPreference = shortlistPreference;
    private readonly IShortlist _shortlist = shortlist;
    private readonly IAudioOutputEnumerator _outputEnumerator = outputEnumerator;
    private readonly IAudioOutput _audioOutput = audioOutput;
    private readonly IControllerSoundCard _controllerSoundCard = controllerSoundCard;
    private readonly SystemCheck _systemCheck = systemCheck;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    // The SHOLTO_MUSIC_DIR environment override, read once by the composition root.
    private readonly string? _musicDirOverride = musicDirOverride;

    // The question currently put to the user, if any: completed by the matching Choose command.
    private TaskCompletionSource<string?>? _musicFolderAnswer;
    private TaskCompletionSource<string?>? _deviceAnswer;
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
    // And for the Glance shortlist.
    private bool _shortlistPersistence;

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

        // Restore the Glance shortlist, the same way; a later toggle is saved once that is done.
        _shortlist.Changed += OnShortlistChanged;
        _ = Task.Run(RestoreShortlistAsync);

        // Resolve which folder to scan. Order: env var override -> saved setting -> first-run picker.
        _ = Task.Run(ResolveMusicDirAsync);

        // Pick audio output device (ask the user on first run or if the saved device is gone).
        _ = StartAudioAsync();
    }

    public void Stop() => _audioOutput.Stop();

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

    // ---- Glance shortlist -----------------------------------------------------------------------

    private async Task RestoreShortlistAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var saved = await _shortlistPreference.GetAsync();
        var paths = ParseShortlist(saved);
        if (paths.Count > 0)
            await _appThread.InvokeAsync(() => _shortlist.Restore(paths));

        _shortlistPersistence = true;
    }

    private IReadOnlyList<string> ParseShortlist(string? saved)
    {
        if (string.IsNullOrEmpty(saved)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(saved) ?? [];
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[Shortlist] saved list unreadable, starting empty: {ex.Message}");
            return [];
        }
    }

    // Raised on the app thread: snapshot the paths now, write on the pool.
    private void OnShortlistChanged()
    {
        if (!_shortlistPersistence) return;
        var json = JsonSerializer.Serialize(_shortlist.Paths);
        _ = Task.Run(async () =>
        {
            try
            {
                await _shortlistPreference.SetAsync(json);
            }
            catch (Exception ex) { Console.WriteLine($"[Shortlist] persist failed: {ex.Message}"); }
        });
    }

    // ---- Music folder --------------------------------------------------------------------------

    private async Task ResolveMusicDirAsync()
    {
        var saved = await _musicDirPreference.GetAsync();
        var musicDir = _musicDirOverride ?? saved;

        // Ask again when there's no saved path (first run) OR the saved path no longer resolves — drive
        // unmounted, or remounted under a different name (/media/s/Data vs /media/s/Data1). Silently
        // skipping left a blank library with no way back short of a restart.
        bool unreachable = !string.IsNullOrEmpty(musicDir) && !Directory.Exists(musicDir);
        if (string.IsNullOrEmpty(musicDir) || unreachable)
        {
            if (unreachable)
            {
                Console.WriteLine($"[Library] saved music dir not reachable: {musicDir} — re-prompting");
                await _appThread.InvokeAsync(() => _library.SetUnreachablePath(musicDir));
            }
            var picked = await RequestMusicFolderAsync(
                unreachable ? MusicFolderReason.DriveNotFound : MusicFolderReason.FirstRun,
                unreachable ? musicDir : null);
            if (string.IsNullOrEmpty(picked)) return;  // cancelled — keep the saved path for next launch
            musicDir = picked;
            await _musicDirPreference.SetAsync(musicDir);
        }

        await _library.ScanAsync(musicDir!, _database.Stores);
    }

    /// <summary>Menu entry point: ask for a new music folder, persist it, then re-scan. Nothing happens if
    /// the user cancels. Re-scans even when the same folder is picked again — important for the banner
    /// re-pick flow, where confirming the same path (after the drive re-mounts) should reload the library.</summary>
    public void Handle(in ChangeMusicFolder command) => _ = ChangeMusicFolderAsync();

    private async Task ChangeMusicFolderAsync()
    {
        if (_musicFolderAnswer is not null) return;  // already asking
        var picked = await RequestMusicFolderAsync(MusicFolderReason.Change, null);
        if (string.IsNullOrEmpty(picked)) return;

        await _musicDirPreference.SetAsync(picked);

        await _library.ScanAsync(picked, _database.Stores);
    }

    public void Handle(in ChooseMusicFolder command)
    {
        var answer = _musicFolderAnswer;
        if (answer is null) return;
        _musicFolderAnswer = null;
        answer.TrySetResult(command.Path);
    }

    private async Task<string?> RequestMusicFolderAsync(MusicFolderReason reason, string? missingPath)
    {
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _appThread.InvokeAsync(() =>
        {
            _musicFolderAnswer = answer;
            _publisher.Publish(new MusicFolderNeeded(reason, missingPath));
        });
        return await answer.Task.ConfigureAwait(false);
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
            chosen = await RequestDeviceAsync(devices, savedName).ConfigureAwait(false);

        // No speaker chosen (cancelled/none): fine if the controller is here to fall back on (master+cue
        // both play from it, like before this feature). Otherwise there's genuinely nothing to play through.
        if (chosen is null && !controllerCardPresent) return;

        if (chosen is not null)
            await _outputDevicePreference.SetAsync(chosen.Name).ConfigureAwait(false);

        await _audioOutput.StartAsync(chosen?.Name).ConfigureAwait(false);
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

        var currentName = await _outputDevicePreference.GetAsync().ConfigureAwait(false);
        var chosen = await RequestDeviceAsync(devices, currentName).ConfigureAwait(false);
        if (chosen is null || chosen.Name == currentName) return;

        await _outputDevicePreference.SetAsync(chosen.Name).ConfigureAwait(false);

        await _audioOutput.SwitchAsync(chosen.Name).ConfigureAwait(false);
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
