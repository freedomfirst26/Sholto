using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Sholto.Analysis;
using Sholto.App.Controls;
using Sholto.App.Theming;
using Sholto.Audio;
using Sholto.Controller.Gestures;
using Sholto.Library;
using Microsoft.Extensions.Options;
using Sholto.Analysis.Reporting;
using Sholto.Analysis.Processing;

namespace Sholto.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IApplication
{
    private int _selectedTrackIndex = -1;
    private SholtoTheme _theme = Themes.SilenceGroove;
    private bool _isMagnetEligible;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The user's music library — owns track rows and scan logic.
    /// MainViewModel observes its <see cref="MusicLibrary.Scanned"/> event to
    /// hook in cross-deck concerns (refresh harmony reference, hydrate stems).</summary>
    public MusicLibrary Library { get; }

    /// <summary>Proxy through to <see cref="Library"/>.Tracks so existing XAML
    /// bindings keep working without churn.</summary>
    public ObservableCollection<TrackRow> Tracks => Library.Tracks;

    /// <summary>Spacebar search overlay state. Built lazily so the
    /// recompute-on-collection-change subscription doesn't fire during MainViewModel
    /// construction (Library.Tracks is still being populated then).</summary>
    public SearchViewModel Search { get; }

    private bool _isSearchOpen;
    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        set
        {
            if (_isSearchOpen == value) return;
            _isSearchOpen = value;
            if (!value) Search.Reset();
            else PickDefaultLoadTarget();
            Notify();
        }
    }

    // Which deck the search overlay will load into on Enter. Set by
    // PickDefaultLoadTarget when the overlay opens (prefer the empty deck;
    // if both loaded, prefer the not-playing one); user can override with
    // ←/→ inside the overlay. Two bool flags drive the two deck-circle
    // symbols' highlight state in XAML.
    private int _loadTargetDeck;
    public int LoadTargetDeck
    {
        get => _loadTargetDeck;
        set
        {
            if (_loadTargetDeck == value) return;
            _loadTargetDeck = value;
            Notify();
            Notify(nameof(IsLoadTargetDeck1));
            Notify(nameof(IsLoadTargetDeck2));
        }
    }
    public bool IsLoadTargetDeck1 => LoadTargetDeck == 0;
    public bool IsLoadTargetDeck2 => LoadTargetDeck == 1;

    /// <summary>Picks which deck the next Enter-in-search will load into.
    /// Priority: (1) the deck that's empty if exactly one is empty,
    /// (2) the deck that's NOT playing if both loaded and exactly one
    /// is playing, (3) deck 1 as fallback (both empty / both playing /
    /// neither playing).</summary>
    public void PickDefaultLoadTarget()
    {
        bool d1L = Deck1.IsLoaded, d2L = Deck2.IsLoaded;
        if (!d1L && !d2L) { LoadTargetDeck = 0; return; }
        if (!d1L) { LoadTargetDeck = 0; return; }
        if (!d2L) { LoadTargetDeck = 1; return; }
        bool d1P = Deck1.IsPlaying, d2P = Deck2.IsPlaying;
        if (d1P && !d2P) { LoadTargetDeck = 1; return; }
        if (d2P && !d1P) { LoadTargetDeck = 0; return; }
        LoadTargetDeck = 0;
    }

    /// <summary>Load the currently-selected library row into the given deck.
    /// Used by Enter in the search overlay AND by 1 / 2 hotkeys / FLX-4 LOAD
    /// buttons. Consolidates the BeginLoad + Decode + LoadTrack flow that
    /// previously lived in MainWindow.</summary>
    public async Task LoadSelectedToDeckAsync(int deckIndex)
    {
        var track = SelectedTrack;
        if (track is null) return;
        var deck = DeckFor(deckIndex);
        var mult = GetBpmMultiplierFor(track.FilePath);
        deck.BeginLoad(track, mult);
        try
        {
            var samples = await Task.Run(() => _decoder.Decode(track.FilePath));
            deck.LoadTrack(track, track.FilePath, samples, mult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Track] load into deck {deckIndex + 1} FAILED: {ex.Message}");
            deck.LoadFailed();
        }
    }

    public TagEditorViewModel? TagEditor { get; private set; }

    /// <summary>Session-only "last selected" memory shared by the tag editor and
    /// the search overlay, so both list recently used tags first.</summary>
    private readonly Sholto.App.Models.TagRecency _tagRecency = new();

    private bool _isTagEditorOpen;
    public bool IsTagEditorOpen
    {
        get => _isTagEditorOpen;
        private set { if (_isTagEditorOpen == value) return; _isTagEditorOpen = value; Notify(); }
    }

    public void AttachTagService(Sholto.Storage.TagService service)
    {
        TagEditor = new TagEditorViewModel(service, _tagRecency);
        TagEditor.RequestClose += () => IsTagEditorOpen = false;
        Notify(nameof(TagEditor));

        service.TagsChanged += async (_, args) =>
        {
            var row = Library.Tracks.FirstOrDefault(r => r.TrackId == args.TrackId);
            if (row is null) return;
            var refreshed = await service.GetTagsForTrackAsync(args.TrackId, default);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => row.Tags = refreshed);
        };

        Search.SetTagService(service, _tagRecency);
        Search.TagPicked += async name =>
        {
            var ids = await service.GetTrackIdsForTagAsync(name, default);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Library.ApplyTagFilter(name, ids);
                IsSearchOpen = false;
            });
        };
    }

    public async Task OpenTagEditorAsync(TrackRow row)
    {
        if (TagEditor is null) return;
        await TagEditor.OpenForAsync(row.TrackId, row.Artist, row.Title);
        IsTagEditorOpen = true;
    }

    // ---- Enter-mode: the track action menu + crate picker ----------------------

    public TrackActionsViewModel TrackActions { get; } = new();
    public CratePickerViewModel? CratePicker { get; private set; }
    private Sholto.Storage.MarkerService? _markerService;

    private bool _isTrackActionsOpen;
    public bool IsTrackActionsOpen
    {
        get => _isTrackActionsOpen;
        private set { if (_isTrackActionsOpen == value) return; _isTrackActionsOpen = value; Notify(); }
    }

    private bool _isCratePickerOpen;
    public bool IsCratePickerOpen
    {
        get => _isCratePickerOpen;
        private set { if (_isCratePickerOpen == value) return; _isCratePickerOpen = value; Notify(); }
    }

    // ---- Controller guide (Faceplate) -------------------------------------------

    /// <summary>The controller guide's own state. Null until <see cref="AttachFaceplate"/>
    /// runs (MainWindow's code-behind does this once the mounted overlay has built its
    /// own view model from <c>FaceplateDocLoader.LoadEmbedded("ddj-flx4")</c>) — there is
    /// deliberately no second, separately-constructed instance here; see the comment on
    /// FaceplateHost in MainWindow.axaml for why that would silently break the panel.</summary>
    public Sholto.Faceplate.ViewModels.FaceplateViewModel? Faceplate { get; private set; }

    /// <summary>Wires up the guide's own view model (see <see cref="Faceplate"/>) once
    /// the overlay hosting it exists. Mirrors <see cref="AttachTagService"/>: state
    /// owned by another part of the app, handed in after construction.</summary>
    public void AttachFaceplate(Sholto.Faceplate.ViewModels.FaceplateViewModel faceplate)
    {
        if (ReferenceEquals(Faceplate, faceplate)) return;
        Faceplate = faceplate;
        Faceplate.RequestClose += () => IsFaceplateOpen = false;
        Notify(nameof(Faceplate));
    }

    private Sholto.Faceplate.FaceplateMount _mount = Sholto.Faceplate.FaceplateMount.Hidden;
    /// <summary>Where the controller guide is shown — see
    /// <see cref="Sholto.Faceplate.FaceplateMount"/>. <see cref="IsFaceplateOpen"/>
    /// derives from this (anything but <c>Hidden</c> counts as open) rather than being
    /// independent state. Opening sets <c>Full</c>; closing sets <c>Hidden</c>.
    /// <c>Embedded</c> is wired but not built — selecting it behaves exactly like
    /// <c>Full</c> until a real embedded layout exists, so it is never a
    /// half-built layout and never a dead value nothing shows.</summary>
    public Sholto.Faceplate.FaceplateMount Mount
    {
        get => _mount;
        set
        {
            if (_mount == value) return;
            var wasOpen = _mount != Sholto.Faceplate.FaceplateMount.Hidden;
            _mount = value;
            var isOpen = _mount != Sholto.Faceplate.FaceplateMount.Hidden;
            Notify();
            Notify(nameof(IsFaceplateOpen));
            if (wasOpen == isOpen) return;
            GestureRouting = isOpen ? GestureRouting.Inspect : GestureRouting.Play;
            // Drop any standing selection without re-raising RequestClose — that event
            // is how the overlay's OWN close button tells us to close; looping back into
            // it here would just re-enter this setter (safely, since the equality check
            // above short-circuits it, but needlessly).
            if (!isOpen) Faceplate?.ClearSelection();
        }
    }

    /// <summary>Whether the controller guide overlay is showing. The only way in is the
    /// top-bar button. Three ways set it back to false: Esc, the overlay's own visible
    /// close button, and the same top-bar button — deliberately NOT a backdrop click,
    /// which would dismiss the whole guide (and whatever was selected) on a click that
    /// merely missed a small control by a few pixels. A thin bool view over
    /// <see cref="Mount"/>: true sets <c>Full</c>, false sets <c>Hidden</c>, and
    /// the Play/Inspect <see cref="GestureRouting"/> flip lives in Mount's setter — Inspect
    /// mode is what lets pressing PLAY on the physical unit explain PLAY instead of
    /// starting a deck.</summary>
    public bool IsFaceplateOpen
    {
        get => Mount != Sholto.Faceplate.FaceplateMount.Hidden;
        set
        {
            if (IsFaceplateOpen == value) return;
            Mount = value ? Sholto.Faceplate.FaceplateMount.Full : Sholto.Faceplate.FaceplateMount.Hidden;
        }
    }

    private GestureRouting _gestureRouting = GestureRouting.Play;
    /// <summary>Whether gestures currently reach the decks (<c>Play</c>) or are being
    /// explained instead (<c>Inspect</c>). Driven purely by <see cref="Mount"/> (via
    /// <see cref="IsFaceplateOpen"/>). <see cref="GestureRoutingChanged"/> is what
    /// App.axaml.cs listens to, to flip the app's and the guide's gesture-binding
    /// tables and repair the LEDs on the way back to Play.</summary>
    public GestureRouting GestureRouting
    {
        get => _gestureRouting;
        private set
        {
            if (_gestureRouting == value) return;
            _gestureRouting = value;
            Notify();
            GestureRoutingChanged?.Invoke(value);
        }
    }

    /// <summary>Fires whenever <see cref="GestureRouting"/> changes. App.axaml.cs wires
    /// this to enable/disable its own and the Faceplate's <c>GestureBindings</c> tables
    /// on the shared <c>GestureBus</c>, and to repair the LEDs when routing returns to
    /// Play (see <see cref="Orchestrator.ReassertLights"/>).</summary>
    public event Action<GestureRouting>? GestureRoutingChanged;

    private string? _toast;
    /// <summary>Brief confirmation text (e.g. "Added to Warmup Set"); null = hidden.</summary>
    public string? Toast
    {
        get => _toast;
        private set { _toast = value; Notify(); Notify(nameof(HasToast)); }
    }
    public bool HasToast => !string.IsNullOrEmpty(_toast);

    private void WireTrackActions()
    {
        TrackActions.RequestClose += () => IsTrackActionsOpen = false;
        TrackActions.Invoked += kind =>
        {
            var row = TrackActions.Row;
            IsTrackActionsOpen = false;
            if (row is null) return;
            switch (kind)
            {
                case TrackActionKind.Tag:        _ = OpenTagEditorAsync(row); break;
                case TrackActionKind.AddToCrate: _ = OpenCratePickerAsync(row); break;
            }
        };
    }

    /// <summary>Enter on a library row opens the action menu for it.</summary>
    public void OpenTrackActions(TrackRow row)
    {
        TrackActions.Open(row);
        IsTrackActionsOpen = true;
    }

    public async Task OpenCratePickerAsync(TrackRow row)
    {
        if (CratePicker is null) return;
        await CratePicker.OpenAsync(row);
        IsCratePickerOpen = true;
    }

    /// <summary>Wire the crate + marker services once the DB is up (mirrors
    /// <see cref="AttachTagService"/>). Enables Enter→Add-to-crate and the CRATES
    /// section of search.</summary>
    public void AttachCrateService(Sholto.Storage.CrateService crate)
    {
        CratePicker = new CratePickerViewModel(crate);
        CratePicker.RequestClose += () => IsCratePickerOpen = false;
        CratePicker.Added += (crateName, title) =>
        {
            ShowToast($"Added to {crateName}");
            Console.WriteLine($"[Crate] added \"{title}\" to \"{crateName}\"");
        };
        Notify(nameof(CratePicker));

        // "All Tracks" is a real crate every song belongs to. New songs that appear
        // in the folder raise Library.TracksAdded → we file them in. On each scan we
        // also reconcile (backfill) any track not yet a member.
        async Task FileIntoAllTracksAsync(IEnumerable<Guid> ids)
        {
            var list = ids as IReadOnlyCollection<Guid> ?? ids.ToList();
            if (list.Count == 0) return;
            int crateId = await crate.CreateAsync("All Tracks"); // get-or-create
            foreach (var id in list) await crate.AddTrackAsync(crateId, id);
        }
        Library.TracksAdded += ids => { _ = FileIntoAllTracksAsync(ids); };
        Library.Scanned += _path => { _ = FileIntoAllTracksAsync(Tracks.Select(r => r.TrackId).ToList()); };

        Search.SetCrateService(crate);
        Search.CratePicked += async c =>
        {
            var ids = await crate.TrackIdsAsync(c.Id);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Library.ApplyCrateFilter(c.Name, ids);
                IsSearchOpen = false;
            });
        };
    }

    public void AttachMarkerService(Sholto.Storage.MarkerService marker)
    {
        _markerService = marker;
        // Load a track's saved markers onto its deck whenever a load completes,
        // across every load path (keyboard, controller, Enter-mode).
        Deck1.LoadStateChanged += s => { if (s == DeckLoadState.Loaded) _ = LoadMarkersForDeckAsync(Deck1); };
        Deck2.LoadStateChanged += s => { if (s == DeckLoadState.Loaded) _ = LoadMarkersForDeckAsync(Deck2); };
    }

    private async Task LoadMarkersForDeckAsync(DeckViewModel deck)
    {
        if (_markerService is null) return;
        var path = deck.LoadedTrack?.FilePath;
        var row = Tracks.FirstOrDefault(r => r.FilePath == path);
        if (row is null) return;
        var markers = await _markerService.ListForTrackAsync(row.TrackId);
        var secs = markers.Select(m => m.PositionSecs).ToArray();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => deck.SetMarkers(secs));
    }

    /// <summary>Drop a marker on the target deck at its current playback position and
    /// persist it (M key; Shift = Deck 2). Refreshes the deck's marker overlay.</summary>
    public async Task AddMarkerToTargetDeckAsync(int deckIndex)
    {
        if (_markerService is null) return;
        var deck = DeckFor(deckIndex);
        if (!deck.Player.IsLoaded) return;
        var path = deck.LoadedTrack?.FilePath;
        var row = Tracks.FirstOrDefault(r => r.FilePath == path);
        if (row is null) return;
        double secs = deck.Player.PositionFrames / (double)Sholto.Audio.AudioFileDecoder.TargetSampleRate;
        await _markerService.AddAsync(row.TrackId, secs);
        await LoadMarkersForDeckAsync(deck);
        ShowToast($"Marker · Deck {deckIndex + 1} · {TimeSpan.FromSeconds(secs):m\\:ss}");
    }

    private void ShowToast(string text)
    {
        Toast = text;
        _ = Task.Run(async () =>
        {
            await Task.Delay(1800);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Toast = null);
        });
    }

    /// <summary>Per-app-run session state — which tracks have been loaded into a
    /// deck so the library can italicise them. Owned here because both decks
    /// produce "played" events and the same row consumes them.</summary>
    public Session Session { get; } = new();

    public DeckViewModel Deck1 { get; }
    public DeckViewModel Deck2 { get; }

    /// <summary>Single reporter instance shared by both decks. Anywhere in the app can
    /// listen to <see cref="IAnalysisReporter.Updated"/> to surface per-track progress.
    /// "beats" (madmom) is the only required step — see <see cref="AnalysisSteps.Beats"/>
    /// and <see cref="AnalysisReport.HasRequiredFailure"/>. Built by the composition root
    /// (App.axaml.cs) and passed in, rather than constructed here, so the same instance
    /// can also be handed to <see cref="IDeckFactory"/> before MainViewModel exists — see
    /// the constructor.</summary>
    public IAnalysisReporter Reporter { get; }

    private string? _debugStats;
    /// <summary>Top-bar CPU/RAM readout when SHOLTO_DEBUG_STATS=1. Null otherwise — the
    /// bound TextBlock auto-hides via its IsVisible binding on string-empty.</summary>
    public string? DebugStats
    {
        get => _debugStats;
        set { _debugStats = value; Notify(); Notify(nameof(DebugStatsVisible)); }
    }
    public bool DebugStatsVisible => !string.IsNullOrEmpty(_debugStats);

    /// <summary>Proxies for the unreachable-banner XAML bindings. The real state
    /// lives on <see cref="Library"/>; we re-emit the PropertyChanged here so
    /// existing bindings on the MainViewModel didn't need to change paths.</summary>
    public string? LibraryUnreachablePath
    {
        get => Library.UnreachablePath;
        set => Library.UnreachablePath = value;
    }
    public bool LibraryUnreachableVisible => Library.IsUnreachable;

    /// <summary>Controller USB connection state, surfaced as a top-bar indicator.
    /// The App feeds this from Controller.ConnectionChanged; the reconnect
    /// supervisor keeps trying, so "Reconnect USB" is a prompt to unplug/replug
    /// when auto-recovery can't (device fully gone from the bus).</summary>
    private bool _controllerConnected;
    public bool ControllerConnected
    {
        get => _controllerConnected;
        set
        {
            if (_controllerConnected == value) return;
            _controllerConnected = value;
            Notify(nameof(ControllerConnected));
            Notify(nameof(ControllerStatusText));
        }
    }
    public string ControllerStatusText => _controllerConnected ? "Controller" : "Reconnect USB";

    private readonly FeatureOptions _features;
    private readonly IAudioFileDecoder _decoder;
    private readonly IThemeContext _themeContext;
    private readonly DemucsStemPresence _stemCache;
    private readonly Sholto.Analysis.IHarmonicKeys _harmonicKeys;
    private readonly Sholto.Analysis.IKeyAnalyzer _keyAnalyzer;

    public MainViewModel(IOptions<FeatureOptions> features,
                         IAudioFileDecoder decoder, IDeckFactory deckFactory,
                         IThemeContext themeContext, DemucsStemPresence stemCache,
                         IAnalysisReporter reporter,
                         Sholto.Library.ITrackScanner trackScanner,
                         Sholto.Analysis.IHarmonicKeys harmonicKeys,
                         Sholto.Analysis.IKeyAnalyzer keyAnalyzer,
                         Sholto.Analysis.ISongSegmentAnalyzer songSegmentAnalyzer)
    {
        _features = features.Value;
        _decoder = decoder;
        _themeContext = themeContext;
        _stemCache = stemCache;
        Reporter = reporter;
        _harmonicKeys = harmonicKeys;
        _keyAnalyzer = keyAnalyzer;
        Library = new MusicLibrary(_themeContext, trackScanner, _harmonicKeys);

        // Make the initial theme visible to anything that reads ThemeContext
        // before the user picks a different theme.
        _themeContext.Current = _theme;

        Search = new SearchViewModel(Tracks);
        WireTrackActions();

        // Re-emit Library's PropertyChanged for the proxied banner properties
        // so XAML bindings on the MainViewModel light up without each control
        // having to subscribe to Library directly.
        Library.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MusicLibrary.UnreachablePath))
            {
                Notify(nameof(LibraryUnreachablePath));
                Notify(nameof(LibraryUnreachableVisible));
            }
        };
        // After every scan: refresh the harmony reference and walk the new rows
        // to see which already have stems on disk. Same cross-deck wiring as
        // before, just hung off the typed event instead of inlined in App.axaml.cs.
        Library.Scanned += scannedPath => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RefreshHarmonyReference();
            _ = HydrateStemStateAsync();
        });

        Deck1 = new DeckViewModel(deckFactory.Create(), _themeContext, _harmonicKeys, songSegmentAnalyzer) { SectionMapEnabled = _features.ShowSectionMap };
        Deck2 = new DeckViewModel(deckFactory.Create(), _themeContext, _harmonicKeys, songSegmentAnalyzer) { SectionMapEnabled = _features.ShowSectionMap };
        Deck1.PersistBpmMultiplier = RaiseBpmMultiplierChanged;
        Deck2.PersistBpmMultiplier = RaiseBpmMultiplierChanged;
        WireDeck(Deck1);
        WireDeck(Deck2);
        WireSessionPlayedTracking(Deck1);
        WireSessionPlayedTracking(Deck2);
        // Route Session events into the matching TrackRow so the library
        // re-renders the italic style.
        Session.TrackPlayed += filePath =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                    if (row.FilePath == filePath) row.IsPlayed = true;
            });
        };

        // Surface analysis progress AND failure on the row. Reading only IsBusy here
        // was the bug that hid five days of broken demucs runs: a step flipping to
        // Failed makes IsBusy go false exactly like success, so the row just quietly
        // stopped spinning and showed nothing at all.
        //
        // Gate the failure computation on HasFailure: Updated fires on every single
        // PropertyChanged from any step (including plain progress ticks), and
        // FailureMessage does a lock + LINQ + string.Join over every step. Without
        // this gate, every demucs tqdm tick on the analyser thread pays for that
        // work — see MainWindow.axaml:217 for why this hot path is already known
        // to be sensitive.
        Reporter.Updated += report =>
        {
            var busy = report.IsBusy;
            var hasFailure = report.HasFailure;
            var failure = hasFailure ? report.FailureMessage : null;
            var requiredFailure = hasFailure && report.HasRequiredFailure;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                {
                    if (row.FilePath != report.FilePath) continue;
                    row.IsAnalyzing = busy;
                    row.AnalysisFailure = failure;
                    row.HasRequiredFailure = requiredFailure;
                }
            });
        };
    }

    /// <summary>Hook a deck's load-lifecycle event into the session. When the
    /// deck transitions to <see cref="DeckLoadState.Loaded"/>, the track on it
    /// gets marked as played. Routed through the deck's typed
    /// <see cref="DeckViewModel.LoadStateChanged"/> event rather than polling
    /// or hooking into LoadTrack itself — keeps the deck logic ignorant of
    /// session state.</summary>
    private void WireSessionPlayedTracking(DeckViewModel deck)
    {
        deck.LoadStateChanged += state =>
        {
            if (state != DeckLoadState.Loaded) return;
            var path = deck.LoadedTrack?.FilePath;
            if (!string.IsNullOrEmpty(path)) Session.MarkPlayed(path!);
        };
    }

    private void WireDeck(DeckViewModel deck)
    {
        deck.Player.AnalysisUpdated += () =>
        {
            var path = deck.LoadedTrack?.FilePath;
            if (path is null) return;
            var bpm = deck.Analysis.Basic?.Bpm;
            var stems = deck.Analysis.Get<StemPaths>();
            var key = deck.Analysis.Get<KeyAnalysis>()?.Camelot;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                {
                    if (row.FilePath != path) continue;
                    if (bpm is not null) row.Bpm = bpm;
                    if (stems is not null) row.StemsReady = true;
                    if (!string.IsNullOrEmpty(key)) row.Key = key;
                }
                RefreshHarmonyReference();
            });
        };
    }

    public SholtoTheme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            // Publish to the process-wide hook so anything not in our visual tree
            // (e.g. value converters) can see the change too.
            _themeContext.Current = value;
            Notify();
            // Re-emit theme-derived bindings on each track and deck so KeyBrush
            // re-evaluates against the new palette. Cheaper than a static event
            // subscription (which would pin every TrackRow until app exit).
            foreach (var row in Tracks) row.RefreshThemeBindings();
            Deck1.RefreshThemeBindings();
            Deck2.RefreshThemeBindings();
            // Typed event so App.axaml.cs can persist the new selection without
            // having to filter the generic PropertyChanged stream.
            ThemeChanged?.Invoke(value);
        }
    }

    /// <summary>Fires when <see cref="Theme"/> changes. App.axaml.cs subscribes
    /// to persist the choice into the settings table so it survives restarts.</summary>
    public event Action<SholtoTheme>? ThemeChanged;

    public int SelectedTrackIndex
    {
        get => _selectedTrackIndex;
        set { _selectedTrackIndex = value; Notify(); Notify(nameof(SelectedTrack)); }
    }

    public Track? SelectedTrack =>
        SelectedTrackIndex >= 0 && SelectedTrackIndex < Tracks.Count
            ? Tracks[SelectedTrackIndex].Track
            : null;

    public TrackRow? SelectedTrackRow =>
        SelectedTrackIndex >= 0 && SelectedTrackIndex < Tracks.Count
            ? Tracks[SelectedTrackIndex]
            : null;

    public void SelectTrack(int index)
    {
        if (Tracks.Count == 0) return;
        SelectedTrackIndex = Math.Clamp(index, 0, Tracks.Count - 1);
    }

    public void OnBrowseRotated(int delta)
    {
        if (Tracks.Count == 0) return;
        int next = SelectedTrackIndex < 0 ? 0 : SelectedTrackIndex + delta;
        SelectTrack(next);
    }

    /// <summary>Look up any persisted ½ / ×2 override for this file path.</summary>
    public double GetBpmMultiplierFor(string filePath)
    {
        foreach (var row in Tracks)
            if (row.FilePath == filePath) return row.BpmMultiplier;
        return 1.0;
    }

    public void OnBrowsePressed(Func<Track, float[]> decodeTrack)
    {
        if (SelectedTrack is null) return;
        var sel = SelectedTrack;
        var mult = GetBpmMultiplierFor(sel.FilePath);
        Deck1.BeginLoad(sel, mult);
        var samples = decodeTrack(sel);
        Deck1.LoadTrack(sel, sel.FilePath, samples, mult);
    }

    /// <summary>Long-press on the browse / song-select button: force-reanalyze the
    /// highlighted track. Recomputes BPM/beats/peaks (BasicAnalysis) AND the Camelot
    /// key, then overwrites the matching cache tiers. Updates the library row in
    /// place and re-broadcasts the harmony reference so dimming refreshes.</summary>
    public async Task OnBrowseHeldAsync(
        Func<Track, float[]> decodeTrack,
        Sholto.Analysis.IAnalysisProvider analysisProvider,
        Func<string, Sholto.Analysis.KeyAnalysis, Task>? saveKey = null)
    {
        var track = SelectedTrack;
        if (track is null) return;

        try
        {
            var samples = await Task.Run(() => decodeTrack(track));
            int rate = Sholto.Audio.AudioFileDecoder.TargetSampleRate;

            var decodedTrack = new Sholto.Analysis.DecodedTrack(track.FilePath, samples, rate, Sholto.Audio.AudioFileDecoder.TargetChannels);
            var basicTask = analysisProvider.RecomputeAsync(decodedTrack);
            var keyTask = _keyAnalyzer.AnalyzeAsync(
                decodedTrack, reporter: Reporter);

            var analysis = await basicTask;
            var key = await keyTask;
            if (saveKey is not null)
            {
                try { await saveKey(track.FilePath, key); }
                catch (Exception ex) { Console.WriteLine($"[MainVM] re-analyze key cache write failed: {ex.Message}"); }
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                {
                    if (row.FilePath != track.FilePath) continue;
                    row.Bpm = analysis.Bpm;
                    if (!string.IsNullOrEmpty(key.Camelot)) row.Key = key.Camelot;
                }
                RefreshHarmonyReference();
            });
            Console.WriteLine($"[MainVM] re-analyzed {track.FilePath}: {analysis.Bpm:F1} BPM, key {key.Camelot}");
        }
        catch (Exception ex)
        {
            // Surface it on the row as well as in the log — a re-analysis that throws
            // here never touches the reporter, so without this the row would silently
            // keep whatever it had and the user would never learn it didn't run.
            Console.WriteLine($"[MainVM] re-analyze failed: {ex.Message}");
            var failure = $"{ex.GetType().Name}: {ex.Message}";
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                    if (row.FilePath == track.FilePath) row.AnalysisFailure = failure;
            });
        }
    }

    /// <summary>Raised when the user asks to re-analyze the highlighted track from the
    /// library (double-click a row). The orchestration layer handles it with the same
    /// decode + AnalysisProvider.Recompute path as the browse-knob long-press, since
    /// only it holds the provider and DB factory. See <see cref="OnBrowseHeldAsync"/>.</summary>
    public event Action? ReanalyzeSelectedRequested;

    /// <summary>Fire <see cref="ReanalyzeSelectedRequested"/> for the current selection.</summary>
    public void RequestReanalyzeSelected() => ReanalyzeSelectedRequested?.Invoke();

    public DeckViewModel DeckFor(int deck) => deck == 1 ? Deck2 : Deck1;

    private double _crossfader = 0.5;
    /// <summary>0..1, 0 = full Deck 1, 1 = full Deck 2. Applies equal-power gains to each deck.</summary>
    public double Crossfader
    {
        get => _crossfader;
        set
        {
            _crossfader = Math.Clamp(value, 0.0, 1.0);
            // Equal-power crossfade: cosine curve so perceived loudness stays flat
            // through the centre. Each deck combines this with its own channel-fader gain.
            EqualPowerCrossfade.ComputeGains(_crossfader, out float gainA, out float gainB);
            Deck1.SetCrossfadeGain(gainA);
            Deck2.SetCrossfadeGain(gainB);
            Notify();
        }
    }

    /// <summary>Raised instead of an instant pause when a playing, scratch-capable
    /// deck is paused — the Orchestrator runs a vinyl-style brake (speed ramps to
    /// zero, pitch falling) and pauses when the platter "stops".</summary>
    public event Action<int>? BrakePauseRequested;

    public void OnPlayPressed(int deck)
    {
        var d = DeckFor(deck);
        if (d.Player.IsPlaying && d.Player.CanScratch)
        {
            BrakePauseRequested?.Invoke(deck);
            return;
        }
        d.Player.TogglePlay();
    }

    public void SetKnownBpms(IReadOnlyDictionary<string, double> bpms)
    {
        foreach (var row in Tracks)
            if (bpms.TryGetValue(row.FilePath, out var bpm)) row.Bpm = bpm;
    }

    /// <summary>Hydrate per-track BPM overrides (½ / ×2 corrections for madmom
    /// half/double-tempo mistakes) from the database into the track rows.</summary>
    public void SetKnownBpmMultipliers(IReadOnlyDictionary<string, double> multipliers)
    {
        foreach (var row in Tracks)
            if (multipliers.TryGetValue(row.FilePath, out var m)) row.BpmMultiplier = m;
    }

    /// <summary>Hydrate cached Camelot keys from the database into the rows at startup.</summary>
    public void SetKnownKeys(IReadOnlyDictionary<string, string> keys)
    {
        foreach (var row in Tracks)
            if (keys.TryGetValue(row.FilePath, out var k)) row.Key = k;
        RefreshHarmonyReference();
    }

    /// <summary>Camelot key of whichever deck is the harmony anchor — Deck 1 if it
    /// has a loaded key, else Deck 2. Drives row dimming in the library list.</summary>
    public string? HarmonyReferenceKey { get; private set; }

    /// <summary>Recompute the reference key from the current deck state and push
    /// it into every row so HarmonyOpacity refreshes.</summary>
    public void RefreshHarmonyReference()
    {
        var anchor = Deck1.Analysis.Get<KeyAnalysis>()?.Camelot
                  ?? Deck2.Analysis.Get<KeyAnalysis>()?.Camelot;
        if (anchor == HarmonyReferenceKey) return;
        HarmonyReferenceKey = anchor;
        foreach (var row in Tracks) row.ReferenceKey = anchor;
        Notify(nameof(HarmonyReferenceKey));
    }

    /// <summary>Raised by deck VMs when the user halves/doubles the BPM of a loaded
    /// track. The app subscribes and persists to SQLite.</summary>
    public event Action<string, double>? BpmMultiplierChanged;
    internal void RaiseBpmMultiplierChanged(string filePath, double multiplier)
    {
        // Also update the matching TrackRow so the library list reflects the change.
        foreach (var row in Tracks)
            if (row.FilePath == filePath) row.BpmMultiplier = multiplier;
        BpmMultiplierChanged?.Invoke(filePath, multiplier);
    }

    /// <summary>Walks every track and asks the stem cache if its 4 WAVs are already
    /// on disk; flips <see cref="TrackRow.StemsReady"/> for the hits. Runs off the
    /// UI thread because each check hashes 1 MiB of the file.</summary>
    public Task HydrateStemStateAsync()
    {
        var rows = Tracks.ToArray();  // snapshot so we don't race the collection
        return Task.Run(() =>
        {
            foreach (var row in rows)
            {
                bool cached;
                try { cached = _stemCache.Contains(row.FilePath); }
                catch { cached = false; }
                if (cached)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => row.StemsReady = true);
            }
        });
    }

    // The magnetism state machine (eligibility, MagnetismFactor, the engage →
    // release → quantize sequence, and Quantize() itself) moved to Orchestrator —
    // it's computed from jog recency, which Orchestrator now owns (JogTracker),
    // and it drives DeckViewModel state (MagneticGlowSec, tempo/phase snaps) the
    // same way Orchestrator's other tick-driven behaviour does. See the split
    // plan in ~/Projects/sholto.md ("split IDeckHost into real roles").
    //
    /// <summary>Notifying mirror of Orchestrator's magnet-lock eligibility check.
    /// XAML binds to this so the centerline magnet glyph can pop in / out via a
    /// style-class transition. Pushed from Orchestrator via
    /// <see cref="Orchestrator.MagnetEligibilityChanged"/> (wired in the App
    /// composition root) each time eligibility flips — not self-computed any
    /// more, since it now depends on jog recency that lives in Orchestrator.</summary>
    public bool IsMagnetEligible
    {
        get => _isMagnetEligible;
        set { if (_isMagnetEligible == value) return; _isMagnetEligible = value; Notify(); }
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
