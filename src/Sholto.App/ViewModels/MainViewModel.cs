using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Sholto.Analysis;
using Sholto.App.Controls;
using Sholto.App.Theming;
using Sholto.Audio;
using Sholto.Music;
using Microsoft.Extensions.Options;

namespace Sholto.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private int _selectedTrackIndex = -1;
    private SholtoTheme _theme = Themes.SilenceGroove;
    private bool _isMagnetEligible;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The user's music library — owns track rows and scan logic.
    /// MainViewModel observes its <see cref="MusicLibrary.Scanned"/> event to
    /// hook in cross-deck concerns (refresh harmony reference, hydrate stems).</summary>
    public MusicLibrary Library { get; } = new();

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
            var samples = await Task.Run(() => Sholto.Audio.AudioFileDecoder.Decode(track.FilePath));
            deck.LoadTrack(track, track.FilePath, samples, mult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Track] load into deck {deckIndex + 1} FAILED: {ex.Message}");
            deck.LoadFailed();
        }
    }

    public TagEditorViewModel? TagEditor { get; private set; }

    private bool _isTagEditorOpen;
    public bool IsTagEditorOpen
    {
        get => _isTagEditorOpen;
        private set { if (_isTagEditorOpen == value) return; _isTagEditorOpen = value; Notify(); }
    }

    public void AttachTagService(Sholto.Storage.TagService service)
    {
        TagEditor = new TagEditorViewModel(service);
        TagEditor.RequestClose += () => IsTagEditorOpen = false;
        Notify(nameof(TagEditor));

        service.TagsChanged += async (_, args) =>
        {
            var row = Library.Tracks.FirstOrDefault(r => r.TrackId == args.TrackId);
            if (row is null) return;
            var refreshed = await service.GetTagsForTrackAsync(args.TrackId, default);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => row.Tags = refreshed);
        };

        Search.SetTagService(service);
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
                case TrackActionKind.LoadDeck1:  _ = LoadSelectedToDeckAsync(0); break;
                case TrackActionKind.LoadDeck2:  _ = LoadSelectedToDeckAsync(1); break;
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
    /// listen to <see cref="AnalysisReporter.Updated"/> to surface per-track progress.</summary>
    public AnalysisReporter Reporter { get; } = new();

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

    private readonly MagnetismOptions _magnetism;

    public MainViewModel(IOptions<MagnetismOptions> magnetism)
    {
        _magnetism = magnetism.Value;

        // Make the initial theme visible to anything that reads ThemeContext
        // before the user picks a different theme.
        ThemeContext.Current = _theme;

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

        Deck1 = new DeckViewModel(new Deck { Reporter = Reporter });
        Deck2 = new DeckViewModel(new Deck { Reporter = Reporter });
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

        // Surface any analysis-in-progress on its row's spinner.
        Reporter.Updated += report =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                    if (row.FilePath == report.FilePath) row.IsAnalyzing = report.IsBusy;
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
            ThemeContext.Current = value;
            Notify();
            Notify(nameof(WaveformPalette));
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

    public WaveformPalette WaveformPalette => _theme.WaveformPalette;

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
        Sholto.Analysis.AnalysisProvider analysisProvider,
        Func<string, Sholto.Analysis.KeyAnalysis, Task>? saveKey = null)
    {
        var track = SelectedTrack;
        if (track is null) return;

        try
        {
            var samples = await Task.Run(() => decodeTrack(track));
            int rate = Sholto.Audio.AudioFileDecoder.TargetSampleRate;

            var basicTask = analysisProvider.RecomputeAsync(track.FilePath, samples, rate);
            var keyTask = Sholto.Analysis.KeyAnalyzer.AnalyzeAsync(
                track.FilePath, samples, channels: 2, sampleRate: rate, reporter: Reporter);

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
            Console.WriteLine($"[MainVM] re-analyze failed: {ex.Message}");
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
            // Equal-power crossfade: cosine curve so the perceived loudness stays flat
            // through the centre instead of dipping like a linear crossfade would.
            // Equal-power crossfade: cosine curve so perceived loudness stays flat
            // through the centre. Each deck combines this with its own channel-fader gain.
            double angle = _crossfader * (Math.PI / 2);
            Deck1.SetCrossfadeGain((float)Math.Cos(angle));
            Deck2.SetCrossfadeGain((float)Math.Sin(angle));
            Notify();
        }
    }

    public void OnPlayPressed(int deck) => DeckFor(deck).TogglePlay();

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
                try { cached = DemucsStemAnalyzer.AreCached(row.FilePath); }
                catch { cached = false; }
                if (cached)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => row.StemsReady = true);
            }
        });
    }

    // How close the two decks' tempos must be for the magnet to engage — now
    // configurable via MagnetismOptions (default 1%). See that type for the rationale.

    /// <summary>
    /// Magnet-lock eligibility. True iff:
    /// <list type="bullet">
    ///   <item>both decks have completed basic analysis (BPM + beat grid),</item>
    ///   <item>both decks are actually playing,</item>
    ///   <item>their <em>playback</em> BPMs (source × multiplier × tempo fader)
    ///         are within <see cref="MagnetismOptions.BpmEligibilityTolerance"/>,</item>
    ///   <item>the user isn't currently rotating <em>both</em> jog wheels at
    ///         once (a dual-jog gesture is the user doing something deliberate;
    ///         the magnet should hold off until they release one).</item>
    /// </list>
    /// </summary>
    public bool IsBpmEligibleForMagnetism
    {
        get
        {
            if (!Deck1.HasAnalysis || !Deck2.HasAnalysis) return false;
            if (!Deck1.Player.IsPlaying || !Deck2.Player.IsPlaying) return false;

            double eff1 = Deck1.EffectiveBpm;
            double eff2 = Deck2.EffectiveBpm;
            if (eff1 <= 0 || eff2 <= 0) return false;

            double diff = Math.Abs(eff1 - eff2) / Math.Max(eff1, eff2);
            if (diff > _magnetism.BpmEligibilityTolerance) return false;

            // Both decks being jogged simultaneously → user is in the middle of
            // a manual adjustment, don't surprise them with a lock.
            if (IsActivelyJogging(LastJogAt1) && IsActivelyJogging(LastJogAt2)) return false;

            return true;
        }
    }

    /// <summary>Notifying mirror of <see cref="IsBpmEligibleForMagnetism"/>.
    /// XAML binds to this so the centerline magnet glyph can pop in / out via
    /// a style-class transition. Updated each tick by <see cref="UpdateMagnetism"/>.</summary>
    public bool IsMagnetEligible
    {
        get => _isMagnetEligible;
        private set { if (_isMagnetEligible == value) return; _isMagnetEligible = value; Notify(); }
    }

    /// <summary>
    /// 0..1: 1 when both decks are playing and their nearest beats are in-phase,
    /// 0 when out of the magnetic window. Returns 0 unconditionally when BPMs
    /// aren't eligible — without this gate, two decks running far apart in tempo
    /// would still drift into phase alignment every few bars and trigger a
    /// surprise snap.
    /// </summary>
    public double MagnetismFactor
    {
        get
        {
            if (!IsBpmEligibleForMagnetism) return 0;
            var d1 = Deck1.Analysis.Basic?.DownbeatTimes;
            var d2 = Deck2.Analysis.Basic?.DownbeatTimes;
            if (d1 is null || d1.Length == 0 || d2 is null || d2.Length == 0) return 0;

            double phase1 = Deck1.PlaybackSeconds - Deck1.NearestDownbeatSec();
            double phase2 = Deck2.PlaybackSeconds - Deck2.NearestDownbeatSec();
            double misalign = Math.Abs(phase1 - phase2);

            const double window = 0.15;  // 150 ms — bar-start tolerance is wider than beat-start
            double t = Math.Min(misalign / window, 1);
            return 1 - t * t * (3 - 2 * t);  // smoothstep, 1 at t=0 → 0 at t=1
        }
    }

    /// <summary>Deck most recently nudged by the jog wheel. The other deck acts as the reference.</summary>
    public int LastJoggedDeck { get; set; } = -1;
    /// <summary>Wall-clock of last jog tick — used to detect "user let go" for quantize.</summary>
    public DateTime LastJogAt { get; set; } = DateTime.MinValue;
    /// <summary>Wall-clock of the last jog event on deck 1 specifically. Used so
    /// we can tell "both decks are being touched right now" apart from "one is".</summary>
    public DateTime LastJogAt1 { get; set; } = DateTime.MinValue;
    /// <summary>Wall-clock of the last jog event on deck 2 specifically.</summary>
    public DateTime LastJogAt2 { get; set; } = DateTime.MinValue;

    // How recently a jog event has to have arrived for that deck to count as
    // "actively being adjusted right now". 250 ms matches the existing
    // IsScrubbing window in the position timer.
    private static readonly TimeSpan ActiveJogWindow = TimeSpan.FromMilliseconds(250);

    private bool IsActivelyJogging(DateTime deckLastJog) =>
        deckLastJog != DateTime.MinValue
        && DateTime.UtcNow - deckLastJog < ActiveJogWindow;

    // Quantize state: cleared when decks separate, set once they snap.
    private bool _quantizeFired;
    private const double EngageThreshold = 0.3;     // same as glow threshold — see one, fire one
    private const double DisengageThreshold = 0.15; // hysteresis to avoid re-fire chatter
    private static readonly TimeSpan JogIdleForQuantize = TimeSpan.FromMilliseconds(180);
    // Auto-quantize only counts as "user released a jog gesture" if the jog was
    // recent. Without this window, two decks running at different tempos would
    // eventually drift into alignment and an old jog from minutes ago would
    // trigger a surprise seek.
    private static readonly TimeSpan JogRecencyForQuantize = TimeSpan.FromSeconds(2);

    /// <summary>Push current magnetism state into each deck's MagneticGlowSec for the UI.
    /// Also runs the engaged → release → quantize state machine.</summary>
    public void UpdateMagnetism()
    {
        // Publish the binary eligibility so the centerline magnet glyph
        // pops in/out via its own style-class transition.
        IsMagnetEligible = IsBpmEligibleForMagnetism;

        double f = MagnetismFactor;

        if (f < DisengageThreshold)
            _quantizeFired = false;  // user pulled them apart; re-arm

        // Fire once: greens visible + user let go of the jog for a beat.
        // Crucial gate: ignore if the user hasn't jogged at all this session
        // (LastJogAt = DateTime.MinValue), or if their last jog was so long ago
        // that "the user just let go" isn't a believable framing any more. This
        // is what stops two decks running at different tempos from triggering a
        // surprise seek every time their phases drift into alignment.
        var sinceJog = DateTime.UtcNow - LastJogAt;
        bool userRecentlyReleasedJog =
            LastJogAt != DateTime.MinValue
            && sinceJog > JogIdleForQuantize
            && sinceJog < JogRecencyForQuantize;

        if (!_quantizeFired
            && f >= EngageThreshold
            && userRecentlyReleasedJog)
        {
            Quantize();
            _quantizeFired = true;
        }

        // Show greens whenever engaged AND we haven't snapped yet. Once snapped, the
        // visuals collapse — that's the "locked, hands off" signal.
        bool active = f >= EngageThreshold && !_quantizeFired;
        Deck1.MagneticGlowSec = active ? Deck1.NearestDownbeatSec() : -1;
        Deck2.MagneticGlowSec = active ? Deck2.NearestDownbeatSec() : -1;
    }

    /// <summary>Snap the last-jogged deck to the reference deck — phase aligns
    /// the downbeats AND tempo-locks so the link actually holds. Without the
    /// tempo lock, a fraction-of-a-percent BPM difference (e.g. 176.5 vs 176.6)
    /// would let the decks drift apart immediately after the snap.</summary>
    private void Quantize()
    {
        DeckViewModel adjusted, reference;
        if (LastJoggedDeck == 1 || LastJoggedDeck == 2)
        {
            adjusted  = LastJoggedDeck == 1 ? Deck1 : Deck2;
            reference = LastJoggedDeck == 1 ? Deck2 : Deck1;
        }
        else
        {
            // No jog history — pick whichever deck is further from its own downbeat
            // (the one with more error to correct).
            double e1 = Math.Abs(Deck1.PlaybackSeconds - Deck1.NearestDownbeatSec());
            double e2 = Math.Abs(Deck2.PlaybackSeconds - Deck2.NearestDownbeatSec());
            (adjusted, reference) = e1 > e2 ? (Deck1, Deck2) : (Deck2, Deck1);
        }

        // 1) Tempo-lock: pull the adjusted deck's EffectiveBpm onto the reference.
        //    Done first so the phase math below works against the locked tempo.
        adjusted.MatchEffectiveBpm(reference.EffectiveBpm);

        // 2) Phase-snap: shift adjusted so its next downbeat lands at the same
        //    wall-clock moment as the reference's next downbeat. Math: place
        //    adj at (its nearest downbeat) + (ref's offset past *its* nearest
        //    downbeat). Walks the same number of seconds past a downbeat as
        //    ref, so the next beats fire together — independent of which bar
        //    of either song they happen to be in.
        double refPhase = reference.PlaybackSeconds - reference.NearestDownbeatSec();
        double adjDownbeat = adjusted.NearestDownbeatSec();
        if (adjDownbeat < 0) return;
        double delta = (adjDownbeat + refPhase) - adjusted.PlaybackSeconds;
        if (Math.Abs(delta) > 0.0001) adjusted.Player.SeekRelative(delta);
        Console.WriteLine($"[Magnet] snap: refPhase={refPhase:F4}s adjDownbeat={adjDownbeat:F4}s delta={delta:F4}s | refBpm={reference.EffectiveBpm:F3} adjBpm={adjusted.EffectiveBpm:F3}");
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
