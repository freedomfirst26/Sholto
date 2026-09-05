using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Sholto.Audio;
using Sholto.Analysis;
using Microsoft.EntityFrameworkCore;
using Sholto.Storage;
using Sholto.Storage.Entities;
using Sholto.Controller;
using Sholto.Controller.Mappings;
using Sholto.Music;
using Sholto.App.Theming;
using Sholto.App.ViewModels;
using Sholto.App.Views;
using TrackRow = Sholto.App.ViewModels.TrackRow;

namespace Sholto.App;

public partial class App : Application
{
    private AudioEngine? _audioEngine;
    private Sholto.Controller.Controller? _controller;
    private Orchestrator? _orchestrator;
    private DispatcherTimer? _statsTimer;
    private MainViewModel? _vm;
    private IDbContextFactory<SholtoDbContext>? _factory;
    // Other startup tasks (music-dir resolution, audio init) need the DB to read
    // settings. They await this TCS so they don't race the DB open task.
    private readonly TaskCompletionSource<IDbContextFactory<SholtoDbContext>?> _dbReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel(
                Microsoft.Extensions.Options.Options.Create(new MagnetismOptions()),
                Microsoft.Extensions.Options.Options.Create(new FeatureOptions()));
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
                _factory = await SholtoStorage.OpenAsync();
                Console.WriteLine($"[DB] opened {SholtoStorage.DefaultDbPath()}");

                var tagService = new TagService(_factory);
                var crateService = new CrateService(_factory);
                var markerService = new MarkerService(_factory);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.AttachTagService(tagService);
                    vm.AttachCrateService(crateService);
                    vm.AttachMarkerService(markerService);
                });

                var basicCache = new BasicAnalysisCache(_factory);
                var keyCache   = new KeyAnalysisCache(_factory);
                var gridCache  = new GridAdjustmentCache(_factory);

                var sharedCaches = new IAnalysisCache[]
                {
                    new MemoryAnalysisCache(),
                    basicCache,
                };
                AnalysisProvider MakeProvider() => new(
                    caches: sharedCaches,
                    compute: (path, samples, rate, ct) =>
                        BasicAnalysis.ComputeAsync(path, samples, channels: 2, sampleRate: rate, reporter: vm.Reporter, ct: ct));
                vm.Deck1.Player.AnalysisProvider = MakeProvider();
                vm.Deck2.Player.AnalysisProvider = MakeProvider();

                foreach (var deck in new[] { vm.Deck1.Player, vm.Deck2.Player })
                {
                    deck.KeyCacheGet = keyCache.TryGetAsync;
                    deck.KeyCachePut = keyCache.PutAsync;
                    // Persist manual beatgrid corrections (BPM override +
                    // phase offset) as a tiny per-track row. madmom's
                    // detection stays immutable; the grid is regenerated
                    // from detection + this adjustment on every load.
                    deck.GridAdjustmentPut = gridCache.PutAsync;
                    deck.GridAdjustmentGet = gridCache.TryGetAsync;
                }

                Dictionary<string, double> bpms;
                Dictionary<string, double> mults;
                Dictionary<string, string> keys;
                await using (var db = _factory.CreateDbContext())
                {
                    var basicRows = await db.BasicAnalyses
                        .AsNoTracking()
                        .Select(b => new { b.Track.Path, b.Data })
                        .ToListAsync();
                    bpms = new Dictionary<string, double>(basicRows.Count);
                    foreach (var r in basicRows)
                    {
                        var basic = AnalysisCodec.Decode(r.Data);
                        if (basic is not null) bpms[r.Path] = basic.Bpm;
                    }

                    mults = await db.BpmOverrides
                        .AsNoTracking()
                        .Select(o => new { o.Track.Path, o.Multiplier })
                        .ToDictionaryAsync(x => x.Path, x => x.Multiplier);

                    var keyRows = await db.KeyAnalyses
                        .AsNoTracking()
                        .Select(k => new { k.Track.Path, k.Data })
                        .ToListAsync();
                    keys = new Dictionary<string, string>(keyRows.Count);
                    foreach (var r in keyRows)
                    {
                        var key = KeyAnalysisCodec.Decode(r.Data);
                        if (key is not null && !string.IsNullOrEmpty(key.Camelot)) keys[r.Path] = key.Camelot;
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.SetKnownBpms(bpms);
                    vm.SetKnownBpmMultipliers(mults);
                    vm.SetKnownKeys(keys);
                });

                var factoryRef = _factory;
                vm.BpmMultiplierChanged += async (path, mult) =>
                {
                    try
                    {
                        await using var db = factoryRef.CreateDbContext();
                        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Path == path);
                        if (track is null) return;
                        var existing = await db.BpmOverrides.FindAsync(track.Id);
                        if (Math.Abs(mult - 1.0) < 0.0001)
                        {
                            if (existing is not null) db.BpmOverrides.Remove(existing);
                        }
                        else if (existing is null)
                        {
                            db.BpmOverrides.Add(new BpmOverride { TrackId = track.Id, Multiplier = mult });
                        }
                        else
                        {
                            existing.Multiplier = mult;
                        }
                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex) { Console.WriteLine($"[DB] save bpm override failed: {ex.Message}"); }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] failed to open: {ex.Message}");
            }
            finally
            {
                _dbReady.TrySetResult(_factory);
            }
        });

        // Restore the previously-selected theme + wire persistence so any future
        // theme change writes through to the settings table. Runs on its own
        // task so it doesn't block the music-dir resolution below.
        _ = Task.Run(async () =>
        {
            var factory = await _dbReady.Task;
            if (factory is null) return;

            string? savedName;
            await using (var db = factory.CreateDbContext())
                savedName = (await db.Settings.FindAsync(SettingsKeys.Theme))?.Value;

            if (!string.IsNullOrEmpty(savedName))
            {
                var match = Themes.All.FirstOrDefault(t => t.Name == savedName);
                if (match is not null)
                    await Dispatcher.UIThread.InvokeAsync(() => vm.Theme = match);
                else
                    Console.WriteLine($"[Theme] saved name '{savedName}' no longer exists — keeping default");
            }

            vm.ThemeChanged += theme =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var db = factory.CreateDbContext();
                        var row = await db.Settings.FindAsync(SettingsKeys.Theme);
                        if (row is null) db.Settings.Add(new Setting { Key = SettingsKeys.Theme, Value = theme.Name });
                        else row.Value = theme.Name;
                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex) { Console.WriteLine($"[Theme] persist failed: {ex.Message}"); }
                });
            };
        });

        // Resolve which folder to scan. Order: env var override → saved setting →
        // first-run picker. The picker fires on the UI thread once the main window
        // is up so the user can see what they're choosing for.
        _ = Task.Run(async () =>
        {
            var factory = await _dbReady.Task;
            string? saved = null;
            if (factory is not null)
            {
                await using var db = factory.CreateDbContext();
                saved = (await db.Settings.FindAsync(SettingsKeys.MusicDir))?.Value;
            }
            var musicDir = Environment.GetEnvironmentVariable("SHOLTO_MUSIC_DIR") ?? saved;

            // Re-prompt when there's no saved path (first run) OR the saved path no
            // longer resolves — drive unmounted, or remounted under a different name
            // (/media/s/Data vs /media/s/Data1). Silently skipping left a blank
            // library with no way back short of a restart.
            bool unreachable = !string.IsNullOrEmpty(musicDir) && !Directory.Exists(musicDir);
            if (string.IsNullOrEmpty(musicDir) || unreachable)
            {
                if (unreachable)
                {
                    Console.WriteLine($"[Library] saved music dir not reachable: {musicDir} — re-prompting");
                    await Dispatcher.UIThread.InvokeAsync(() => vm.Library.UnreachablePath = musicDir);
                }
                var title = unreachable
                    ? "Music drive not found — reconnect it, then choose your library"
                    : "Choose your music library";
                var picked = await Dispatcher.UIThread.InvokeAsync(async () =>
                    await PickMusicDirAsync(desktop.MainWindow!, title));
                if (string.IsNullOrEmpty(picked)) return;  // cancelled — keep saved path for next launch
                musicDir = picked;
                if (factory is not null)
                {
                    await using var db = factory.CreateDbContext();
                    var row = await db.Settings.FindAsync(SettingsKeys.MusicDir);
                    if (row is null) db.Settings.Add(new Setting { Key = SettingsKeys.MusicDir, Value = musicDir });
                    else row.Value = musicDir;
                    await db.SaveChangesAsync();
                }
            }

            await vm.Library.ScanAsync(musicDir, _factory);
        });

        // Pick audio output device (prompt user on first run or if saved device is gone)
        _ = StartAudioAsync(vm, desktop);

        // DdjFlx4Options is a plain POCO owned by Sholto.Controller (that project
        // doesn't take a Microsoft.Extensions.Options dependency); App builds it
        // through IOptions like the other *Options types, then hands the .Value
        // straight to the Controller, which threads it down to the mapping.
        var flx4Options = Microsoft.Extensions.Options.Options.Create(new DdjFlx4Options());
        _controller = new Sholto.Controller.Controller(flx4Options.Value);
        if (!_controller.Connect())
            Console.WriteLine("DDJ-FLX4 not found — use UI controls.");

        _orchestrator = new Orchestrator(vm, () => _factory,
            Microsoft.Extensions.Options.Options.Create(new ScratchOptions()));
        _controller.Action += evt => Dispatcher.UIThread.Post(() => _orchestrator.HandleControllerEvent(evt));
        // Surface controller connection state in the top-bar indicator. Seed with
        // the result of the first attempt above, then follow the supervisor's events.
        vm.ControllerConnected = _controller.IsConnected;
        _controller.ConnectionChanged += connected =>
            Dispatcher.UIThread.Post(() => vm.ControllerConnected = connected);
        // App→controller output: orchestrator asks, controller lights the LED.
        _orchestrator.BeatSyncLightRequested += (deck, on) => _controller.SetBeatSync(deck, on);
        _orchestrator.PadLightRequested += (deck, group, on) => _controller.SetPadLight(deck, group, on);
        _orchestrator.EchoLightRequested += (deck, on) => _controller.SetEchoLight(deck, on);
        _orchestrator.MasterCueRequested += on => _audioEngine?.SetMasterCue(on);

        // Known state on boot: every button LED off + cue audio cleared, emitted
        // after Action is wired so the cleared-cue events reach the Session.
        _controller.Reset();
        _orchestrator.Start();

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
            _orchestrator?.Dispose();
            _statsTimer?.Stop();
            _audioEngine?.Stop();
            _controller?.Dispose();
        };
    }

    private async Task StartAudioAsync(MainViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var allDevices = await Task.Run(() => AudioDevices.EnumerateOutputs());
        if (allDevices.Count == 0)
        {
            Console.WriteLine("No audio output devices found.");
            return;
        }

        // The DDJ-FLX4 is never a pickable "master speaker" — AudioEngine
        // auto-selects it as the device to open (4ch: master 1-2 + headphone
        // cue 3-4) whenever it's connected, and the picker only offers where
        // to re-route master to. See AudioEngine.Start for the full story.
        bool flx4Present = allDevices.Any(d => PipeWireRouter.IsFlx4(d.Name));
        var devices = allDevices.Where(d => !PipeWireRouter.IsFlx4(d.Name)).ToList();

        var factory = await _dbReady.Task;
        string? savedName = null;
        if (factory is not null)
        {
            await using var db = factory.CreateDbContext();
            savedName = (await db.Settings.FindAsync(SettingsKeys.OutputDevice))?.Value;
        }
        var chosen = devices.FirstOrDefault(d => d.Name == savedName);

        if (chosen is null && devices.Count > 0)
            chosen = await PromptForDeviceAsync(devices, savedName, desktop.MainWindow!);

        // No speaker chosen (cancelled/none): fine if the FLX4 is here to fall
        // back on (master+cue both play from it, like before this feature).
        // Otherwise there's genuinely nothing to play through.
        if (chosen is null && !flx4Present) return;

        if (chosen is not null && factory is not null)
        {
            await using var db = factory.CreateDbContext();
            var row = await db.Settings.FindAsync(SettingsKeys.OutputDevice);
            if (row is null) db.Settings.Add(new Setting { Key = SettingsKeys.OutputDevice, Value = chosen.Name });
            else row.Value = chosen.Name;
            await db.SaveChangesAsync();
        }

        await Task.Run(() =>
        {
            try
            {
                var engine = new AudioEngine(vm.Deck1.Player, vm.Deck2.Player);
                engine.Start(chosen?.Name);
                _audioEngine = engine;
                Console.WriteLine($"Audio engine started; master speaker={(chosen?.Name ?? "(FLX4, no separate speaker chosen)")}");
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

    /// <summary>Menu entry point: prompt for a new music folder, persist it,
    /// then re-scan. No-ops if the user cancels.
    /// Note: re-scans even if the user picks the same folder that's already
    /// saved — important for the banner re-pick flow, where confirming the
    /// same path (after the drive re-mounts) should reload the library.</summary>
    public async Task ChangeMusicDirAsync(Avalonia.Controls.Window owner)
    {
        if (_vm is null) return;
        var picked = await PickMusicDirAsync(owner, "Choose your music library");
        if (string.IsNullOrEmpty(picked)) return;

        var factory = await _dbReady.Task;
        if (factory is not null)
        {
            await using var db = factory.CreateDbContext();
            var row = await db.Settings.FindAsync(SettingsKeys.MusicDir);
            if (row is null) db.Settings.Add(new Setting { Key = SettingsKeys.MusicDir, Value = picked });
            else row.Value = picked;
            await db.SaveChangesAsync();
        }

        await _vm.Library.ScanAsync(picked, _factory);
    }

    public async Task ChangeOutputDeviceAsync(Avalonia.Controls.Window owner)
    {
        if (_vm is null) return;

        // Excludes the FLX4 — see StartAudioAsync's comment. If it's the only
        // device connected there's nothing to offer; leave master on it.
        var allDevices = await Task.Run(() => AudioDevices.EnumerateOutputs());
        var devices = allDevices.Where(d => !PipeWireRouter.IsFlx4(d.Name)).ToList();
        if (devices.Count == 0) return;

        var factory = await _dbReady.Task;
        string? currentName = null;
        if (factory is not null)
        {
            await using var db = factory.CreateDbContext();
            currentName = (await db.Settings.FindAsync(SettingsKeys.OutputDevice))?.Value;
        }
        var chosen = await PromptForDeviceAsync(devices, currentName, owner);
        if (chosen is null || chosen.Name == currentName) return;

        if (factory is not null)
        {
            await using var db = factory.CreateDbContext();
            var row = await db.Settings.FindAsync(SettingsKeys.OutputDevice);
            if (row is null) db.Settings.Add(new Setting { Key = SettingsKeys.OutputDevice, Value = chosen.Name });
            else row.Value = chosen.Name;
            await db.SaveChangesAsync();
        }

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
