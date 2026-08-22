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
using Sholto.Library;
using Sholto.App.Theming;
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
    private IDbContextFactory<SholtoDbContext>? _factory;
    // Other startup tasks (music-dir resolution, audio init) need the DB to read
    // settings. They await this TCS so they don't race the DB open task.
    private readonly TaskCompletionSource<IDbContextFactory<SholtoDbContext>?> _dbReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    // Jog-wheel scrubs are coalesced per frame so we issue one Seek per deck per ~16 ms.
    private double _pendingJog1, _pendingJog2;
    // Browse-button long-press: hold for 1s to force-reanalyze the highlighted track.
    private DispatcherTimer? _browseHoldTimer;

    // Per-deck Shift modifier state (deck 1 / deck 2). The FLX-4 emits
    // combined events (e.g. Shift+Left → ch=5 0x4A) without disclosing
    // which deck's Shift was held — we track press/release here so the
    // NudgeGrid handler can route to the right deck.
    private readonly bool[] _shiftHeld = new bool[2];

    // Stem-level modifier (deck-agnostic). Hold the dedicated FLX-4 button
    // (ch=5 0x47) and the 3 EQ knobs on both decks become per-stem
    // attenuators (HI → Drums, MID → Vocals, LOW → Instrumental).
    private bool _stemLevelMode;

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
                _factory = await SholtoStorage.OpenAsync();
                Console.WriteLine($"[DB] opened {SholtoStorage.DefaultDbPath()}");

                var tagService = new TagService(_factory);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => vm.AttachTagService(tagService));

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

            if (string.IsNullOrEmpty(musicDir))
            {
                musicDir = await Dispatcher.UIThread.InvokeAsync(async () =>
                    await PickMusicDirAsync(desktop.MainWindow!, "Choose your music library"));
                if (!string.IsNullOrEmpty(musicDir) && factory is not null)
                {
                    await using var db = factory.CreateDbContext();
                    var row = await db.Settings.FindAsync(SettingsKeys.MusicDir);
                    if (row is null) db.Settings.Add(new Setting { Key = SettingsKeys.MusicDir, Value = musicDir });
                    else row.Value = musicDir;
                    await db.SaveChangesAsync();
                }
            }
            else if (!Directory.Exists(musicDir))
            {
                Console.WriteLine($"[Library] saved music dir not reachable: {musicDir} — skipping scan");
                await Dispatcher.UIThread.InvokeAsync(() => vm.Library.UnreachablePath = musicDir);
                return;
            }

            if (string.IsNullOrEmpty(musicDir)) return;
            await vm.Library.ScanAsync(musicDir, _factory);
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
                        // Short tap: no-op (Load 1 / Load 2 buttons do the loading).
                        // Long press (≥1 s): force-reanalyze the highlighted track —
                        // rescue path for tracks whose cached BPM/beats are wrong.
                        // Some controllers retransmit NoteOn while held; if a timer is
                        // already counting, leave it alone instead of resetting it.
                        if (_browseHoldTimer is null)
                        {
                            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                            timer.Tick += (_, _) =>
                            {
                                timer.Stop();
                                if (ReferenceEquals(_browseHoldTimer, timer)) _browseHoldTimer = null;
                                var provider = vm.Deck1.Player.AnalysisProvider;
                                if (provider is null) { Console.WriteLine("[App] browse-hold: no AnalysisProvider yet"); return; }
                                Console.WriteLine($"[App] browse-hold fired → re-analyzing {vm.SelectedTrack?.FilePath}");
                                _ = vm.OnBrowseHeldAsync(
                                    t => AudioFileDecoder.Decode(t.FilePath),
                                    provider,
                                    saveKey: _factory is not null
                                        ? (path, key) => new KeyAnalysisCache(_factory!).PutAsync(path, key)
                                        : null);
                            };
                            _browseHoldTimer = timer;
                            timer.Start();
                        }
                        break;
                    case ControllerEvent.BrowseReleased:
                        _browseHoldTimer?.Stop();
                        _browseHoldTimer = null;
                        break;
                    case ControllerEvent.LoadToDeck l:
                    {
                        var sel = vm.SelectedTrack;
                        if (sel is not null)
                        {
                            var deck = vm.DeckFor(l.Deck);
                            var mult = vm.GetBpmMultiplierFor(sel.FilePath);
                            deck.BeginLoad(sel, mult);
                            _ = Task.Run(async () =>
                            {
                                var samples = AudioFileDecoder.Decode(sel.FilePath);
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                    deck.LoadTrack(sel, sel.FilePath, samples, mult));
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
                    case ControllerEvent.CueToggle ct:
                        vm.DeckFor(ct.Deck).ToggleCue();
                        break;
                    case ControllerEvent.EqMoved e:
                    {
                        // While the dedicated stem-level button (ch=5 0x47)
                        // is held, the 3 EQ knobs on BOTH decks are
                        // repurposed as stem-group attenuators (HI → Drums,
                        // MID → Vocals, LOW → Instrumental). Going via the
                        // VM so the level flows through DeckViewModel for
                        // any future UI hooks (the visual fade was reverted
                        // — VM properties drive audio only for now).
                        if (_stemLevelMode)
                        {
                            var deckVm = vm.DeckFor(e.Deck);
                            switch (e.Band)
                            {
                                case EqBand.High: deckVm.DrumsLevel        = e.Value; break;
                                case EqBand.Mid:  deckVm.VocalsLevel       = e.Value; break;
                                default:          deckVm.InstrumentalLevel = e.Value; break;
                            }
                        }
                        else
                        {
                            vm.DeckFor(e.Deck).Player.SetEq((int)e.Band, e.Value);
                        }
                        break;
                    }
                    case ControllerEvent.FilterMoved f:
                        vm.DeckFor(f.Deck).Player.SetFilter(f.Position);
                        break;
                    case ControllerEvent.TempoMoved t:
                        vm.DeckFor(t.Deck).SetTempoPosition(t.Position);
                        break;
                    case ControllerEvent.StemToggle st:
                    {
                        var deckVm = vm.DeckFor(st.Deck);
                        // VM bools are the source of truth for "is this group on now".
                        // Flip them, then push the new gain into the audio path.
                        bool nextActive = st.Group switch
                        {
                            0 => !deckVm.DrumsActive,
                            1 => !deckVm.VocalsActive,
                            _ => !deckVm.InstrumentalActive,
                        };
                        switch (st.Group)
                        {
                            case 0: deckVm.DrumsActive        = nextActive; break;
                            case 1: deckVm.VocalsActive       = nextActive; break;
                            case 2: deckVm.InstrumentalActive = nextActive; break;
                        }
                        deckVm.Player.SetStemGroup(st.Group, nextActive);
                        break;
                    }
                    case ControllerEvent.BeatLoopToggle bl:
                        vm.DeckFor(bl.Deck).Player.EnableBeatLoop(bl.Bars);
                        break;
                    case ControllerEvent.BeatLoopHalve bh:
                        vm.DeckFor(bh.Deck).Player.HalveLoop();
                        break;
                    case ControllerEvent.BeatLoopDouble bd:
                        vm.DeckFor(bd.Deck).Player.DoubleLoop();
                        break;
                    case ControllerEvent.DeckShift ds:
                        _shiftHeld[ds.Deck] = ds.Pressed;
                        break;
                    case ControllerEvent.StemLevelMode sm:
                        _stemLevelMode = sm.Pressed;
                        break;
                    case ControllerEvent.BeatSyncPressed:
                        // Plain BEAT SYNC — beat-sync not yet implemented.
                        break;
                    case ControllerEvent.SetDownbeatHere sd:
                        vm.DeckFor(sd.Deck).Player.SetDownbeatAtPlayhead();
                        break;
                    case ControllerEvent.CyclePitchRange cpr:
                        // Shift + BEAT SYNC: ±6 → ±10 → ±16 → WIDE.
                        vm.DeckFor(cpr.Deck).CyclePitchRange();
                        break;
                    case ControllerEvent.NudgeGrid n:
                    {
                        // Explicit deck wins.
                        if (n.Deck >= 0) { vm.DeckFor(n.Deck).Player.NudgeGrid(n.Beats); break; }

                        // The FLX-4's Shift+Left/Right arrives without a deck,
                        // but the deck's Shift was held while the button fired
                        // — that tells us where to route. Prefer Shift state;
                        // fall back to active-loop deck if no Shift is held
                        // (the keyboard path also routes here).
                        int target;
                        if (_shiftHeld[0]) target = 0;
                        else if (_shiftHeld[1]) target = 1;
                        else if (vm.DeckFor(0).Player.ActiveLoop is not null) target = 0;
                        else if (vm.DeckFor(1).Player.ActiveLoop is not null) target = 1;
                        else target = 0;
                        vm.DeckFor(target).Player.NudgeGrid(n.Beats);
                        break;
                    }
                    case ControllerEvent.JogRotated j:
                    {
                        // Loop is locked: the jog wheel is ignored entirely while a
                        // loop is active. Otherwise scrubbing could pull the playhead
                        // outside the loop region and break the wrap.
                        if (vm.DeckFor(j.Deck).Player.ActiveLoop is not null) break;

                        // Accumulate the jog delta; the 60 Hz timer below flushes it
                        // into a single Seek per frame. Each Seek causes SoundFlow to
                        // flush its audio buffer; firing one per event (the wheel sends
                        // ~100/sec) turns into audible glitching.
                        // Side ring is the slow / fine seek; 0.00125 s = ¼× the previous
                        // 0.005 s — finer control for nudging beat alignment.
                        double secsPerTick = j.Source == JogSource.TopPlatter ? 0.05 : 0.00125;
                        if (j.Deck == 0) _pendingJog1 += j.Delta * secsPerTick;
                        else             _pendingJog2 += j.Delta * secsPerTick;
                        vm.LastJoggedDeck = j.Deck == 0 ? 1 : 2;
                        var nowUtc = DateTime.UtcNow;
                        vm.LastJogAt = nowUtc;
                        // Per-deck timestamps so MainViewModel can tell "both
                        // decks being touched right now" apart from "just one".
                        if (j.Deck == 0) vm.LastJogAt1 = nowUtc;
                        else             vm.LastJogAt2 = nowUtc;
                        break;
                    }
                }
            });
        };

        // Position sync at 60 Hz so rotation + waveform scroll look smooth.
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

        var factory = await _dbReady.Task;
        string? savedName = null;
        if (factory is not null)
        {
            await using var db = factory.CreateDbContext();
            savedName = (await db.Settings.FindAsync(SettingsKeys.OutputDevice))?.Value;
        }
        var chosen = devices.FirstOrDefault(d => d.Name == savedName);

        if (chosen is null)
            chosen = await PromptForDeviceAsync(devices, savedName, desktop.MainWindow!);

        if (chosen is null) return;

        if (factory is not null)
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

        var devices = await Task.Run(() => AudioDevices.EnumerateOutputs());
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
