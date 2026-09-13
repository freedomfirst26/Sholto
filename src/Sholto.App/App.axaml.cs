using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Sholto.Audio;
using Sholto.Analysis;
using Sholto.ExternalTools;
using Microsoft.EntityFrameworkCore;
using Sholto.Storage;
using Sholto.Storage.Entities;
using Sholto.Controller;
using Sholto.Controller.Gestures;
using Sholto.Controller.Mappings;
using Sholto.Library;
using Sholto.App.Theming;
using Sholto.App.ViewModels;
using Sholto.App.Views;
using TrackRow = Sholto.App.ViewModels.TrackRow;

namespace Sholto.App;

public partial class App : Application
{
    private AudioEngine? _audioEngine;
    private Sholto.Controller.IControlSurface? _controller;
    private Orchestrator? _orchestrator;
    private GestureRecognizer? _recognizer;
    private GestureBus? _bus;
    private GestureBindings? _appBindings;
    private GestureBindings? _faceplateBindings;
    private DispatcherTimer? _statsTimer;
    // Resolves its initial theme through Avalonia's AssetLoader (avares://) — see
    // IThemeContext's doc — so, unlike every other leaf below, it cannot be built
    // in Program.BuildAvaloniaApp's factory lambda (runs before Initialize(), with
    // no live Avalonia application to load bundled themes through). Stays here.
    private IThemeContext? _themeContext;
    private ProcessStats? _processStats;
    private MainViewModel? _vm;
    private IDbContextFactory<SholtoDbContext>? _factory;
    private Controller.Mappings.IControllerMappings? _mappingRegistry;
    private Controller.MidiManager? _midiManager;
    // Other startup tasks (music-dir resolution, audio init) need the DB to read
    // settings. They await this TCS so they don't race the DB open task.
    private readonly TaskCompletionSource<IDbContextFactory<SholtoDbContext>?> _dbReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Which world this process runs against — real FLX4 + real window + real view
    // model, or (later) a scripted stand-in. Chosen once, at the entry point
    // (Program.BuildAvaloniaApp), and handed in here; App itself never picks. The
    // factory only ASSEMBLES the three entities Orchestrator binds — every leaf
    // collaborator below is still built by this class. See ISholtoEntities.
    private readonly ISholtoEntities _entities;

    // The plain-.NET leaves (tool stack, decoder, analysis stack, deck factory,
    // storage, device enumeration, track scanner) — built in Program.BuildAvaloniaApp,
    // before Avalonia's own setup runs, and handed in here. See SholtoStack for what's
    // inside, why it had to move up, and why _themeContext above did NOT move with it.
    private readonly SholtoStack _stack;

    public App() : this(new LiveEntities(), SholtoStack.Build()) { }

    public App(ISholtoEntities entities, SholtoStack stack)
    {
        _entities = entities;
        _stack = stack;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Every leaf below except _themeContext was built in
            // Program.BuildAvaloniaApp, before this method (and before
            // Initialize()) ever ran — see SholtoStack for what's inside, in
            // what order, and why. demucsCache is read out here because it
            // (unlike the rest of the tool stack) is only needed for the
            // ApplicationLeaves bundle just below, not by anything already
            // baked into _stack.
            var demucsCache = _stack.ToolStack.StemCache;

            _themeContext = new ThemeContext();

            // Leaves are done. Hand the app-side bundle to the entity factory and let
            // IT assemble the application + keyboard pair — that pairing (the window's
            // DataContext IS the application Orchestrator is given) is the factory's
            // whole reason to exist, and used to be an unguaranteed
            // `(IKeyboard)desktop.MainWindow!` cast down in InitializeServices.
            // The leaves only mean anything to the live assembly; a scripted one
            // brings its own, which is why this is not on ISholtoEntities.
            if (_entities is LiveEntities live)
                live.UseApplicationLeaves(new ApplicationLeaves(
                    SholtoOptions.Default.Feature,
                    _stack.Decoder, _stack.DeckFactory, _themeContext, demucsCache, _stack.AnalysisStack.Reporter,
                    _stack.TrackScanner, _stack.AnalysisStack.HarmonicKeys, _stack.AnalysisStack.KeyAnalyzer,
                    _stack.AnalysisStack.SongSegments));

            // The one remaining concrete coupling: App's own startup work (library
            // scan, theme, known-BPM seeding, debug stats) is MainViewModel-specific
            // and deliberately NOT on IApplication — IApplication is the four roles
            // Orchestrator needs, not everything the app can do.
            var vm = (MainViewModel)_entities.CreateApplication();
            _vm = vm;
            desktop.MainWindow = (Avalonia.Controls.Window)_entities.CreateKeyboard();

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
                _factory = await _stack.Storage.OpenAsync();
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

                // Build the 3 DB-backed tiers and point the switchable
                // wrappers at them. Both decks already hold those SAME
                // switchable instances (handed to DeckFactory before the DB
                // existed) — nothing here reaches back into Deck1.Player/
                // Deck2.Player; the very temporal coupling IDeckFactory exists
                // to remove. AttachDatabase is the one mutation in this whole
                // path, and it mutates the switchable wrappers, never Deck.
                // See AnalysisStack's class doc for what silently breaks if
                // this call is ever skipped.
                _stack.AnalysisStack.AttachDatabase(_factory);

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
        var flx4Options = SholtoOptions.Default.DdjFlx4;
        _mappingRegistry = new Controller.Mappings.MappingRegistry(flx4Options.Value);
        _midiManager = new Controller.MidiManager(_mappingRegistry)
        {
            LogAllMessages = Environment.GetEnvironmentVariable("SHOLTO_MIDI_LOG") == "1",
        };
        // Leaves again: the MIDI stack is App's to build (mappings + manager above),
        // the control surface made out of it is the factory's to assemble.
        if (_entities is LiveEntities live) live.UseMidi(_midiManager);
        _controller = _entities.CreateControlSurface();
        if (!_controller.Connect())
            Console.WriteLine("DDJ-FLX4 not found — use UI controls.");

        _recognizer = new GestureRecognizer();
        // The three entities Orchestrator glues, all three from the same factory, so
        // they are consistent by construction: the control surface (just created),
        // the keyboard (the same MainWindow already on screen — see IKeyboard), and
        // the app (vm, which composes the four IApplication roles).
        var keyboard = _entities.CreateKeyboard();
        _orchestrator = new Orchestrator(_controller, keyboard, vm, () => _factory,
            SholtoOptions.Default.Scratch,
            SholtoOptions.Default.Magnetism,
            _recognizer, _stack.Decoder);
        // Orchestrator now owns magnetism eligibility (moved out of MainViewModel
        // along with the jog state it's computed from — see the split plan in
        // ~/Projects/sholto.md). It has no MainViewModel reference (only
        // IApplication), so it pushes the UI-bound flag back via this event, the
        // same pattern MasterCueRequested uses below.
        _orchestrator.MagnetEligibilityChanged += eligible => vm.IsMagnetEligible = eligible;

        _bus = new GestureBus();
        _appBindings = new GestureBindings("app", _orchestrator.BuildGestureTable());
        _bus.Register(_appBindings);
        // The controller guide's own table on the SAME bus, same vocabulary, different
        // delegate — every gesture selects and blinks the control that owns it (see
        // FaceplateViewModel.OnLiveGesture) instead of acting on the deck. Starts
        // disabled; GestureRoutingChanged below flips it in lockstep with the app's own
        // table, one on while the other is off, so the two are never both live and the
        // guide never receives a gesture no one is showing it to.
        _faceplateBindings = new GestureBindings("faceplate",
            GestureIds.All.ToDictionary(id => id, _ => new Action<Gesture>(g => vm.Faceplate?.OnLiveGesture(g))))
        {
            Enabled = false,
        };
        _bus.Register(_faceplateBindings);
        _bus.HandlerFailed += (table, g, ex) =>
            Console.WriteLine($"[Gestures] '{table.Name}' threw on {g.Id}: {ex.Message}");

        // Inspect mode (guide open) silences the decks and hands every gesture to the
        // guide instead; Play mode is the reverse.
        //
        // The Controller lights its own buttons on press, BEFORE it raises the event
        // the App sees (see Controller.OnCueClicked/OnMasterCueClicked), so disabling
        // the app's gesture table for Inspect mode cannot stop an LED changing: press
        // headphone CUE while the guide is open and the unit's LED flips while the
        // App's real cue state never moves — controller and screen disagree. On the
        // way back to Play we call Controller.Reset() (blanks every LED) then
        // Orchestrator.ReassertLights() (repaints BEAT SYNC / stem-mute / echo from
        // what the App actually knows) to fix that.
        //
        // But Reset() is also destructive to the cue buttons specifically — it forces
        // both decks' headphone cue AND master cue off, with no memory of what they
        // were. Left alone, "cue deck 2, open the guide, close the guide" would leave
        // the LED and the app's cue-active flag agreeing, but agreeing on the wrong
        // (dead) state — a DJ mid-set coming back to a killed cue. So we snapshot the
        // cue buttons' real on/off state on the way INTO Inspect (before anything has
        // touched them) and restore it on the way out, through RestoreCueState — which
        // re-raises CueChanged/MasterCueChanged exactly as a physical press would, so
        // the audio routing follows too, not just the LED. Reset() also blanks the
        // pad-mode (HOT CUE / PAD FX1) LEDs without forgetting which page is actually
        // active, so ReassertPadPages() repaints those from the Controller's own
        // memory the same way.
        Sholto.Controller.CueSnapshot cueSnapshot = default;
        vm.GestureRoutingChanged += routing =>
        {
            var inspect = routing == GestureRouting.Inspect;
            _appBindings.Enabled = !inspect;
            _faceplateBindings.Enabled = inspect;
            if (inspect)
            {
                cueSnapshot = _controller!.SnapshotCueState();
            }
            else
            {
                _controller!.Reset();
                _orchestrator!.ReassertLights();
                // A platter grabbed just before Inspect opened never gets its lift
                // event (App's own gesture table was disabled) — clear any scratch
                // state Inspect stranded so the deck isn't left silenced. See
                // Orchestrator.ReleaseStrandedScratchTouches.
                _orchestrator.ReleaseStrandedScratchTouches();
                _controller.ReassertPadPages();
                _controller.RestoreCueState(cueSnapshot);
            }
        };

        _controller.Action += evt => Dispatcher.UIThread.Post(() =>
        {
            var gesture = _recognizer!.Recognize(evt, DateTime.UtcNow);
            if (gesture is not null) _bus.Dispatch(gesture);
        });

        // The browse hold becomes due through time, not through an event.
        _orchestrator.Ticked += () =>
        {
            foreach (var due in _recognizer.Tick(DateTime.UtcNow))
                _bus.Dispatch(due);
        };
        // Surface controller connection state in the top-bar indicator. Seed with
        // the result of the first attempt above, then follow the supervisor's events.
        vm.ControllerConnected = _controller.IsConnected;
        _controller.ConnectionChanged += connected =>
            Dispatcher.UIThread.Post(() => vm.ControllerConnected = connected);
        // Orchestrator now drives BEAT SYNC/pad/echo LEDs directly against the
        // control surface it was constructed with (the LED relay moved out of
        // App and into Orchestrator — see Orchestrator.ReassertLights). Master
        // cue still goes through an event: its destination is the audio engine,
        // not the control surface, and Orchestrator holds no reference to it.
        _orchestrator.MasterCueRequested += on => _audioEngine?.SetMasterCue(on);

        // Known state on boot: every button LED off + cue audio cleared, emitted
        // after Action is wired so the cleared-cue events reach the Tool.
        _controller.Reset();
        _orchestrator.Start();

        // SHOLTO_DEBUG_STATS=1 → top-right CPU/RAM readout, sampled once per second.
        _processStats = new ProcessStats();
        if (_processStats.Enabled)
        {
            // Warm-up read so the first displayed value isn't garbage from the
            // long since-startup interval.
            _ = _processStats.Sample();
            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statsTimer.Tick += (_, _) => vm.DebugStats = _processStats.SampleString();
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
        var allDevices = await Task.Run(() => _stack.AudioDevices.EnumerateOutputs());
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
                var engine = new AudioEngine([_stack.FlacStrategy], _stack.PipeWireRouter, vm.Deck1.Player, vm.Deck2.Player);
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
        var allDevices = await Task.Run(() => _stack.AudioDevices.EnumerateOutputs());
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
                    var engine = new AudioEngine([_stack.FlacStrategy], _stack.PipeWireRouter, _vm.Deck1.Player, _vm.Deck2.Player);
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
