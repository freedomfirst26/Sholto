using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Sholto.Audio;
using Sholto.Analysis;
using Sholto.Storage;
using Sholto.Controller;
using Sholto.Library;
using Sholto.App.ViewModels;
using Sholto.App.Views;
using TrackRow = Sholto.App.ViewModels.TrackRow;

namespace Sholto.App;

public partial class App : Application
{
    private AudioEngine? _audioEngine;
    private MidiManager? _midi;
    private DispatcherTimer? _positionTimer;
    private DispatcherTimer? _statsTimer;
    private MainViewModel? _vm;
    private SholtoDatabase? _db;
    // Jog-wheel scrubs are coalesced per frame so we issue one Seek per deck per ~16 ms.
    private double _pendingJog1, _pendingJog2;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            _vm = vm;
            desktop.MainWindow = new Views.MainWindow { DataContext = vm };

            // Let the window paint its first frame, THEN initialize services.
            // Posting at Background priority ensures Render runs before InitializeServices.
            desktop.MainWindow.Opened += (_, _) =>
                Dispatcher.UIThread.Post(() => InitializeServices(vm, desktop),
                    DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices(MainViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Open the library database; persists analysis across runs.
        _ = Task.Run(async () =>
        {
            try
            {
                _db = await SholtoDatabase.OpenAsync();
                Console.WriteLine($"[DB] opened {_db.DatabasePath}");

                // Build one 3-tier provider per deck. They share the same memory + DB cache
                // so analysing a track once benefits whichever deck loads it next.
                var sharedCaches = new IAnalysisCache[]
                {
                    new MemoryAnalysisCache(),
                    new DatabaseAnalysisCache(_db),
                };
                AnalysisProvider MakeProvider() => new(
                    caches: sharedCaches,
                    compute: (path, samples, rate, ct) =>
                        BasicAnalysis.ComputeAsync(path, samples, channels: 2, sampleRate: rate, reporter: null, ct: ct));
                vm.Deck1.Player.AnalysisProvider = MakeProvider();
                vm.Deck2.Player.AnalysisProvider = MakeProvider();

                // Pre-populate the UI's BPM map from cache so tracks light up immediately.
                var bpms = await _db.GetAllBpmsAsync();
                await Dispatcher.UIThread.InvokeAsync(() => vm.SetKnownBpms(bpms));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] failed to open: {ex.Message}");
            }
        });

        // Resolve which folder to scan. Order: env var override → saved config →
        // first-run picker. The picker fires on the UI thread once the main window
        // is up so the user can see what they're choosing for.
        _ = Task.Run(async () =>
        {
            var musicDir = Environment.GetEnvironmentVariable("SHOLTO_MUSIC_DIR")
                           ?? AppConfig.Load().MusicDir;

            if (string.IsNullOrEmpty(musicDir) || !Directory.Exists(musicDir))
            {
                musicDir = await Dispatcher.UIThread.InvokeAsync(async () =>
                    await PickMusicDirAsync(desktop.MainWindow!, "Choose your music library"));
                if (!string.IsNullOrEmpty(musicDir))
                    AppConfig.Update(c => c.MusicDir = musicDir);
            }

            if (string.IsNullOrEmpty(musicDir)) return;  // user cancelled — nothing to scan
            await ScanLibraryAsync(vm, musicDir);
        });

        // Pick audio output device (prompt user on first run or if saved device is gone)
        _ = StartAudioAsync(vm, desktop);

        _midi = new MidiManager { LogAllMessages = true };  // TODO: turn off after mapping
        if (!_midi.Connect())
            Console.WriteLine("DDJ-FLX4 not found — use UI controls.");

        _midi.EventReceived += evt =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                switch (evt)
                {
                    case ControllerEvent.BrowseRotated r:
                        vm.OnBrowseRotated(r.Delta);
                        break;
                    case ControllerEvent.BrowsePressed:
                        // Encoder press: bring focus to the track list; rotation moves selection.
                        // (no track load — Load 1 / Load 2 buttons do that)
                        break;
                    case ControllerEvent.LoadToDeck l:
                    {
                        var sel = vm.SelectedTrack;
                        if (sel is not null)
                        {
                            var deck = vm.DeckFor(l.Deck);
                            _ = Task.Run(async () =>
                            {
                                var samples = AudioFileDecoder.Decode(sel.FilePath);
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                    deck.LoadTrack(sel, sel.FilePath, samples));
                            });
                        }
                        break;
                    }
                    case ControllerEvent.PlayPressed p:
                        vm.OnPlayPressed(p.Deck);
                        break;
                    case ControllerEvent.CrossfaderMoved c:
                        vm.Crossfader = c.Position;
                        break;
                    case ControllerEvent.ChannelVolumeMoved v:
                        vm.DeckFor(v.Deck).ChannelGain = v.Value;
                        break;
                    case ControllerEvent.EqMoved e:
                        vm.DeckFor(e.Deck).Player.SetEq((int)e.Band, e.Value);
                        break;
                    case ControllerEvent.TempoMoved t:
                        vm.DeckFor(t.Deck).SetTempoPosition(t.Position);
                        break;
                    case ControllerEvent.StemToggle st:
                    {
                        var deckVm = vm.DeckFor(st.Deck);
                        bool active = deckVm.Player.ToggleStemGroup(st.Group);
                        switch (st.Group)
                        {
                            case 0: deckVm.DrumsActive       = active; break;
                            case 1: deckVm.VocalsActive      = active; break;
                            case 2: deckVm.InstrumentalActive = active; break;
                        }
                        break;
                    }
                    case ControllerEvent.JogRotated j:
                    {
                        // Accumulate the jog delta; the 60 Hz timer below flushes it
                        // into a single Seek per frame. Each Seek causes SoundFlow to
                        // flush its audio buffer; firing one per event (the wheel sends
                        // ~100/sec) turns into audible glitching.
                        double secsPerTick = j.Source == JogSource.TopPlatter ? 0.05 : 0.005;
                        if (j.Deck == 0) _pendingJog1 += j.Delta * secsPerTick;
                        else             _pendingJog2 += j.Delta * secsPerTick;
                        vm.LastJoggedDeck = j.Deck == 0 ? 1 : 2;
                        vm.LastJogAt = DateTime.UtcNow;
                        break;
                    }
                }
            });
        };

        // Position sync + flush of pending jog scrubs, both at 60 Hz.
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _positionTimer.Tick += (_, _) =>
        {
            // Magnetic beat-snap: when both decks are playing and their nearest beats
            // are close to in-phase, eat up to 90% of every jog tick so the user can
            // "feel" the beat hold them in place.
            double scale = 1 - vm.MagnetismFactor * 0.9;
            if (_pendingJog1 != 0) { vm.Deck1.Player.SeekRelative(_pendingJog1 * scale); _pendingJog1 = 0; }
            if (_pendingJog2 != 0) { vm.Deck2.Player.SeekRelative(_pendingJog2 * scale); _pendingJog2 = 0; }

            // Mark the deck "scrubbing" while jog input was recent — the waveform uses
            // this to switch from short top/bottom stripes to a full-height guide line.
            var now = DateTime.UtcNow;
            vm.Deck1.IsScrubbing = vm.LastJoggedDeck == 1 && (now - vm.LastJogAt) < TimeSpan.FromMilliseconds(250);
            vm.Deck2.IsScrubbing = vm.LastJoggedDeck == 2 && (now - vm.LastJogAt) < TimeSpan.FromMilliseconds(250);

            vm.UpdateMagnetism();

            // Always sync, even when paused or stopped, so keyboard / FLX-4 scrubs
            // are reflected immediately in the playhead.
            if (vm.Deck1.Player.IsLoaded) vm.Deck1.SyncPlayPosition();
            if (vm.Deck2.Player.IsLoaded) vm.Deck2.SyncPlayPosition();
        };
        _positionTimer.Start();

        // SHOLTO_DEBUG_STATS=1 → top-right CPU/RAM readout, sampled once per second.
        if (ProcessStats.Enabled)
        {
            // Warm-up read so the first displayed value isn't garbage from the
            // long since-startup interval.
            _ = ProcessStats.Sample();
            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statsTimer.Tick += (_, _) => vm.DebugStats = ProcessStats.SampleString();
            _statsTimer.Start();
        }

        desktop.Exit += (_, _) =>
        {
            _positionTimer?.Stop();
            _statsTimer?.Stop();
            _audioEngine?.Stop();
            _midi?.Dispose();
        };
    }

    private async Task StartAudioAsync(MainViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var devices = await Task.Run(() => AudioDevices.EnumerateOutputs());
        if (devices.Count == 0)
        {
            Console.WriteLine("No audio output devices found.");
            return;
        }

        var config = AppConfig.Load();
        var chosen = devices.FirstOrDefault(d => d.Name == config.OutputDeviceName);

        if (chosen is null)
            chosen = await PromptForDeviceAsync(devices, config.OutputDeviceName, desktop.MainWindow!);

        if (chosen is null) return;

        AppConfig.Update(c => c.OutputDeviceName = chosen.Name);

        await Task.Run(() =>
        {
            try
            {
                var engine = new AudioEngine(vm.Deck1.Player, vm.Deck2.Player);
                engine.Start(chosen.Name);
                _audioEngine = engine;
                Console.WriteLine($"Audio engine started on: {chosen.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio engine failed to start: {ex.Message}");
            }
        });
    }

    public async Task<AudioDevice?> PromptForDeviceAsync(
        IReadOnlyList<AudioDevice> devices, string? currentName, Avalonia.Controls.Window owner)
    {
        var picker = new AudioDevicePicker(devices, currentName);
        await picker.ShowDialog(owner);
        return picker.SelectedDevice;
    }

    /// <summary>Show the OS folder picker for the user's music library. Returns the
    /// chosen absolute path, or null if they cancelled.</summary>
    public static async Task<string?> PickMusicDirAsync(Avalonia.Controls.Window owner, string title)
    {
        var top = Avalonia.Controls.TopLevel.GetTopLevel(owner);
        if (top is null) return null;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });
        if (folders.Count == 0) return null;
        var uri = folders[0].Path;
        return uri.IsFile ? uri.LocalPath : uri.ToString();
    }

    /// <summary>Run a full library scan from <paramref name="musicDir"/> and hydrate
    /// the view-model with tracks, BPMs from cache, and stem-on-disk state.</summary>
    private async Task ScanLibraryAsync(ViewModels.MainViewModel vm, string musicDir)
    {
        Console.WriteLine($"[Library] scanning {musicDir}");
        var tracks = await TrackScanner.ScanAsync(musicDir);

        Dictionary<string, double>? cachedBpms = null;
        if (_db is not null)
        {
            foreach (var t in tracks) await _db.UpsertTrackAsync(t);
            cachedBpms = await _db.GetAllBpmsAsync();
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            vm.Tracks.Clear();
            foreach (var t in tracks) vm.Tracks.Add(new TrackRow(t));
            if (cachedBpms is not null) vm.SetKnownBpms(cachedBpms);
        });
        await vm.HydrateStemStateAsync();
    }

    /// <summary>Menu entry point: prompt for a new music folder, persist it,
    /// then re-scan. No-ops if the user cancels.</summary>
    public async Task ChangeMusicDirAsync(Avalonia.Controls.Window owner)
    {
        if (_vm is null) return;
        var picked = await PickMusicDirAsync(owner, "Choose your music library");
        if (string.IsNullOrEmpty(picked)) return;

        if (picked == AppConfig.Load().MusicDir) return;
        AppConfig.Update(c => c.MusicDir = picked);

        await ScanLibraryAsync(_vm, picked);
    }

    public async Task ChangeOutputDeviceAsync(Avalonia.Controls.Window owner)
    {
        if (_vm is null) return;

        var devices = await Task.Run(() => AudioDevices.EnumerateOutputs());
        if (devices.Count == 0) return;

        var config = AppConfig.Load();
        var chosen = await PromptForDeviceAsync(devices, config.OutputDeviceName, owner);
        if (chosen is null || chosen.Name == config.OutputDeviceName) return;

        AppConfig.Update(c => c.OutputDeviceName = chosen.Name);

        // Use SoundFlow's runtime device switch (preserves the audio graph).
        await Task.Run(() =>
        {
            try
            {
                if (_audioEngine is null)
                {
                    var engine = new AudioEngine(_vm.Deck1.Player, _vm.Deck2.Player);
                    engine.Start(chosen.Name);
                    _audioEngine = engine;
                }
                else
                {
                    _audioEngine.SwitchDevice(chosen.Name);
                }
                Console.WriteLine($"Audio engine switched to: {chosen.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio engine failed to start: {ex.Message}");
            }
        });
    }
}
