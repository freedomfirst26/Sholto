using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Sholto.App.Analysis.Harmony;
using Sholto.App.ExternalTools;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The main window's view model: overlays, the search text and filter display, the load target
/// choice, the faceplate mount, the theme, the toast and the system report. It talks to the App only
/// through the bus: it subscribes to the library, mixer, controller and toast-worthy events, caches what it
/// shows, and sends commands for every action. Events arrive on the app thread (the UI thread), so the
/// handlers update directly.</summary>
public sealed class MainViewModel :
    INotifyPropertyChanged,
    IEventHandler<SelectionChanged>,
    IEventHandler<LibraryFilterChanged>,
    IEventHandler<LibraryUnreachableChanged>,
    IEventHandler<HarmonyReferenceChanged>,
    IEventHandler<LibraryDatabaseAttached>,
    IEventHandler<CrossfaderChanged>,
    IEventHandler<MagnetEligibilityChanged>,
    IEventHandler<DeviceConnectionChanged>,
    IEventHandler<MarkerAdded>,
    IEventHandler<TrackAddedToCrate>,
    IEventHandler<TrackLoadFailed>
{
    private SholtoTheme _theme;
    private bool _isMagnetEligible;

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly LibraryRowsViewModel _rows;

    /// <summary>The visible library rows; the library list and the search overlay bind to it.</summary>
    public ObservableCollection<TrackRow> Tracks => _rows.Items;

    /// <summary>Spacebar search overlay state. Built lazily so the
    /// recompute-on-collection-change subscription doesn't fire during MainViewModel
    /// construction (the rows are still being populated then).</summary>
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

    /// <summary>Load the currently-selected library row into the given deck. Used by Enter in the search
    /// overlay AND by the 1 / 2 hotkeys / FLX-4 LOAD buttons (which send the same command): the headless
    /// track loader does the work.</summary>
    public void LoadSelectedToDeck(int deckIndex) =>
        _sender.Send(new LoadSelectedIntoDeck(deckIndex, new Origin(InterfaceIds.MainUI, "search", "load")));

    public TagEditorViewModel? TagEditor { get; private set; }

    private bool _isTagEditorOpen;
    public bool IsTagEditorOpen
    {
        get => _isTagEditorOpen;
        private set { if (_isTagEditorOpen == value) return; _isTagEditorOpen = value; Notify(); }
    }

    /// <summary>The database is up (the App announces it): build the tag editor and the crate picker, which
    /// read and edit through queries and commands. (The search overlay follows the same event itself.)</summary>
    void IEventHandler<LibraryDatabaseAttached>.Handle(in LibraryDatabaseAttached e)
    {
        if (!e.Available || TagEditor is not null) return;

        TagEditor = _overlays.TagEditor();
        TagEditor.RequestClose += () => IsTagEditorOpen = false;
        Notify(nameof(TagEditor));

        CratePicker = _overlays.CratePicker();
        CratePicker.RequestClose += () => IsCratePickerOpen = false;
        Notify(nameof(CratePicker));
    }

    public async Task OpenTagEditorAsync(TrackRow row)
    {
        if (TagEditor is null) return;
        await TagEditor.OpenForAsync(row.TrackId, row.Artist, row.Title);
        IsTagEditorOpen = true;
    }

    // ---- Enter-mode: the track action menu + crate picker ----------------------

    public TrackActionsViewModel TrackActions { get; }
    public OutputPickerViewModel OutputPicker { get; }

    private bool _isOutputPickerOpen;
    public bool IsOutputPickerOpen
    {
        get => _isOutputPickerOpen;
        private set { if (_isOutputPickerOpen == value) return; _isOutputPickerOpen = value; Notify(); }
    }
    public CratePickerViewModel? CratePicker { get; private set; }

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
    /// own view model from <c>FaceplateDocLoader.Load(IDeviceFaceplate)</c>) — there is
    /// deliberately no second, separately-constructed instance here; see the MainWindow
    /// constructor for why that would silently break the panel.</summary>
    public Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel? Faceplate { get; private set; }

    /// <summary>Wires up the guide's own view model (see <see cref="Faceplate"/>) once
    /// the overlay hosting it exists. Mirrors <see cref="AttachTagService"/>: state
    /// owned by another part of the app, handed in after construction.</summary>
    public void AttachFaceplate(Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel faceplate)
    {
        if (ReferenceEquals(Faceplate, faceplate)) return;
        Faceplate = faceplate;
        Faceplate.RequestClose += () => IsFaceplateOpen = false;
        Notify(nameof(Faceplate));
    }

    private Sholto.Interface.Faceplate.FaceplateMount _mount = Sholto.Interface.Faceplate.FaceplateMount.Hidden;
    /// <summary>Where the controller guide is shown — see
    /// <see cref="Sholto.Interface.Faceplate.FaceplateMount"/>. <see cref="IsFaceplateOpen"/>
    /// derives from this (anything but <c>Hidden</c> counts as open) rather than being
    /// independent state. Opening sets <c>Full</c>; closing sets <c>Hidden</c>.
    /// <c>Embedded</c> is wired but not built — selecting it behaves exactly like
    /// <c>Full</c> until a real embedded layout exists, so it is never a
    /// half-built layout and never a dead value nothing shows.</summary>
    public Sholto.Interface.Faceplate.FaceplateMount Mount
    {
        get => _mount;
        set
        {
            if (_mount == value) return;
            var wasOpen = _mount != Sholto.Interface.Faceplate.FaceplateMount.Hidden;
            _mount = value;
            var isOpen = _mount != Sholto.Interface.Faceplate.FaceplateMount.Hidden;
            Notify();
            Notify(nameof(IsFaceplateOpen));
            if (wasOpen == isOpen) return;
            // The guide tells the App whether Inspect mode is on (SetInspectMode); the App owns that state.
            Faceplate?.SetMounted(isOpen);
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
    /// <see cref="Mount"/>: true sets <c>Full</c>, false sets <c>Hidden</c>. Mounting the guide
    /// turns Inspect mode on (Mount's setter tells the Faceplate view model, which sends
    /// <c>SetInspectMode</c>): that is what lets pressing PLAY on the physical unit explain PLAY
    /// instead of starting a deck.</summary>
    public bool IsFaceplateOpen
    {
        get => Mount != Sholto.Interface.Faceplate.FaceplateMount.Hidden;
        set
        {
            if (IsFaceplateOpen == value) return;
            Mount = value ? Sholto.Interface.Faceplate.FaceplateMount.Full : Sholto.Interface.Faceplate.FaceplateMount.Hidden;
        }
    }

    private string? _toast;
    /// <summary>Brief confirmation text (e.g. "Added to Warmup Set"); null = hidden.</summary>
    public string? Toast
    {
        get => _toast;
        private set { _toast = value; Notify(); Notify(nameof(HasToast)); }
    }
    public bool HasToast => !string.IsNullOrEmpty(_toast);

    private void WireOutputPicker()
    {
        OutputPicker.Opened += () => IsOutputPickerOpen = true;
        OutputPicker.RequestClose += () => IsOutputPickerOpen = false;
    }

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

    /// <summary>A marker was dropped on a deck (M key; Shift = Deck 2): confirm it with a toast.</summary>
    void IEventHandler<MarkerAdded>.Handle(in MarkerAdded e) =>
        ShowToast($"Marker · Deck {e.Deck + 1} · {TimeSpan.FromSeconds(e.Seconds):m\\:ss}");

    /// <summary>A track went into a crate (from the crate picker): confirm it with a toast.</summary>
    void IEventHandler<TrackAddedToCrate>.Handle(in TrackAddedToCrate e) => ShowToast($"Added to {e.CrateName}");

    /// <summary>A track could not be loaded: the deck stays usable; say so.</summary>
    void IEventHandler<TrackLoadFailed>.Handle(in TrackLoadFailed e) =>
        ShowToast($"Couldn't load {e.Title} · Deck {e.Deck + 1}");

    private void ShowToast(string text)
    {
        Toast = text;
        _ = HideToastLaterAsync();
    }

    /// <summary>The wait runs off the app thread; clearing the toast comes back onto it.</summary>
    private async Task HideToastLaterAsync()
    {
        await Task.Delay(1800).ConfigureAwait(false);
        _appThread.Post(() => Toast = null);
    }

    public DeckViewModel Deck1 { get; }
    public DeckViewModel Deck2 { get; }

    private string? _debugStats;
    /// <summary>Top-bar CPU/RAM readout when SHOLTO_DEBUG_STATS=1. Null otherwise — the
    /// bound TextBlock auto-hides via its IsVisible binding on string-empty.</summary>
    public string? DebugStats
    {
        get => _debugStats;
        set { _debugStats = value; Notify(); Notify(nameof(DebugStatsVisible)); }
    }
    public bool DebugStatsVisible => !string.IsNullOrEmpty(_debugStats);

    /// <summary>The saved music folder that could not be reached (the App's
    /// <see cref="LibraryUnreachableChanged"/>), for the unreachable-banner bindings.</summary>
    private string? _libraryUnreachablePath;
    public string? LibraryUnreachablePath => _libraryUnreachablePath;
    public bool LibraryUnreachableVisible => !string.IsNullOrEmpty(_libraryUnreachablePath);

    void IEventHandler<LibraryUnreachableChanged>.Handle(in LibraryUnreachableChanged e)
    {
        _libraryUnreachablePath = e.Path;
        Notify(nameof(LibraryUnreachablePath));
        Notify(nameof(LibraryUnreachableVisible));
    }

    /// <summary>Controller USB connection state, surfaced as a top-bar indicator.
    /// The App publishes it (<see cref="DeviceConnectionChanged"/>); the reconnect
    /// supervisor keeps trying, so "Reconnect USB" is a prompt to unplug/replug
    /// when auto-recovery can't (device fully gone from the bus).</summary>
    private bool _controllerConnected;
    public bool ControllerConnected => _controllerConnected;

    void IEventHandler<DeviceConnectionChanged>.Handle(in DeviceConnectionChanged e)
    {
        if (_controllerConnected == e.Connected) return;
        _controllerConnected = e.Connected;
        Notify(nameof(ControllerConnected));
        Notify(nameof(ControllerStatusText));
        // Red outranks amber: a dropped USB is urgent and fixable in seconds,
        // a missing tool needs an install and a restart. So the amber flag has
        // to be re-evaluated whenever the controller flips.
        Notify(nameof(SystemDegraded));
    }
    public string ControllerStatusText => _controllerConnected ? "Controller" : "Reconnect USB";

    // ---- Boot-time tool check: amber dot + system report ------------------------

    private SystemCheck? _systemCheck;

    /// <summary>One row per external tool, for the system report overlay. Empty until
    /// <see cref="ReportSystemCheck"/> runs.</summary>
    public ObservableCollection<SystemReportRow> SystemReport { get; } = new();

    /// <summary>Hand the boot-time tool probe's result to the UI. A METHOD, not a
    /// constructor parameter: <c>tools/Sholto.Interface.Bench</c>'s app factory builds this view
    /// model with a fixed argument list, and an extra parameter would break it. The
    /// check is a value already computed by <c>ToolSet</c> at boot — nothing here
    /// re-probes the filesystem, and availability deliberately never goes live
    /// (see ExternalToolFinder: a tool installed mid-session needs a restart).</summary>
    public void ReportSystemCheck(SystemCheck check)
    {
        _systemCheck = check;
        SystemReport.Clear();
        foreach (var tool in check.Tools) SystemReport.Add(new SystemReportRow(tool));
        Notify(nameof(SystemDegraded));
        Notify(nameof(SystemReportHeadline));
    }

    /// <summary>Amber dot: some tool is missing AND the controller is fine. Never true
    /// while the controller is down — red wins, and the dot only ever shows one
    /// colour. Both <c>Degraded</c> and <c>Offline</c> land here; the severity shows
    /// up in <see cref="SystemReportHeadline"/>, not in the colour.</summary>
    public bool SystemDegraded =>
        _controllerConnected && _systemCheck is { Status: not SystemStatus.Healthy };

    /// <summary>What the report leads with — the one place the Offline/Degraded
    /// distinction is visible to the user.</summary>
    public string SystemReportHeadline => _systemCheck?.Status switch
    {
        SystemStatus.Offline =>
            "Beat detection is unavailable, so tracks get no BPM, beatgrid, waveform or key.",
        SystemStatus.Degraded =>
            "Sholto is running, but some optional analysis features are unavailable.",
        _ => "Everything Sholto needs is installed.",
    };

    private bool _isSystemReportOpen;
    /// <summary>Whether the system report overlay is showing. The only way in is a
    /// click on the amber dot; Esc and a backdrop click close it — the same
    /// show/hide shape as the Enter-mode track action menu.</summary>
    public bool IsSystemReportOpen
    {
        get => _isSystemReportOpen;
        private set { if (_isSystemReportOpen == value) return; _isSystemReportOpen = value; Notify(); }
    }

    /// <summary>Click handler's entry point. No-ops unless the dot is actually amber,
    /// so a green (or red) dot is not a hidden button.</summary>
    public void OpenSystemReport()
    {
        if (!SystemDegraded) return;
        IsSystemReportOpen = true;
    }

    public void CloseSystemReport() => IsSystemReportOpen = false;

    private readonly IOverlayViewModelFactory _overlays;
    private readonly IThemeContext _themeContext;
    private readonly IThemeCatalog _themeCatalog;
    private readonly ICommandSender _sender;
    private readonly IAppThread _appThread;

    public MainViewModel(IThemeContext themeContext, IThemeCatalog themeCatalog,
                         IOverlayViewModelFactory overlays,
                         LibraryRowsViewModel rows,
                         ICommandSender sender,
                         IEventSubscriber subscriber,
                         IAppThread appThread,
                         SearchViewModel search,
                         TrackActionsViewModel trackActions,
                         OutputPickerViewModel outputPicker,
                         DeckViewModel deck1,
                         DeckViewModel deck2)
    {
        _themeContext = themeContext;
        _themeCatalog = themeCatalog;
        _theme = themeCatalog.ByName("Silence Groove");
        _overlays = overlays;
        _sender = sender;
        _appThread = appThread;
        _rows = rows;
        TrackActions = trackActions;
        OutputPicker = outputPicker;

        // Make the initial theme visible to anything that reads ThemeContext
        // before the user picks a different theme.
        _themeContext.Current = _theme;

        Search = search;
        // Picking a tag or crate in the search overlay filters the library (a command) and closes the overlay.
        Search.TagPicked += name =>
        {
            _sender.Send(new FilterLibraryByTag(name, Ui("search", "pick-tag")));
            IsSearchOpen = false;
        };
        Search.CratePicked += crate =>
        {
            _sender.Send(new FilterLibraryByCrate(crate.Id, crate.Name, Ui("search", "pick-crate")));
            IsSearchOpen = false;
        };
        WireTrackActions();
        WireOutputPicker();

        Deck1 = deck1;
        Deck2 = deck2;

        // The App announces; this view model caches and re-emits for the bindings. State events are
        // replayed on subscribe, so a view model built after the App started still shows the real picture.
        subscriber.Subscribe<LibraryFilterChanged>(this);
        subscriber.Subscribe<LibraryUnreachableChanged>(this);
        subscriber.Subscribe<HarmonyReferenceChanged>(this);
        subscriber.Subscribe<SelectionChanged>(this);
        subscriber.Subscribe<CrossfaderChanged>(this);
        subscriber.Subscribe<MagnetEligibilityChanged>(this);
        subscriber.Subscribe<DeviceConnectionChanged>(this);
        subscriber.Subscribe<LibraryDatabaseAttached>(this);
        subscriber.Subscribe<MarkerAdded>(this);
        subscriber.Subscribe<TrackAddedToCrate>(this);
        subscriber.Subscribe<TrackLoadFailed>(this);
    }

    private Origin Ui(string control, string gesture) => new(InterfaceIds.MainUI, control, gesture);

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
            _rows.RefreshThemeBindings();
            Deck1.RefreshThemeBindings();
            Deck2.RefreshThemeBindings();
            // Tell the App so it can remember the choice and restore it next launch.
            _sender.Send(new ChooseTheme(value.Name, new Origin(InterfaceIds.MainUI, "theme", "choose")));
        }
    }

    /// <summary>Apply the theme saved last time, if the catalog still has one of that name. Not a user
    /// choice: the App already has the name, and it ignores the resulting <c>ChooseTheme</c> until the
    /// restore is done.</summary>
    public void RestoreTheme(string savedName)
    {
        var match = _themeCatalog.All.FirstOrDefault(t => t.Name == savedName);
        if (match is not null)
            Theme = match;
        else
            Console.WriteLine($"[Theme] saved name '{savedName}' no longer exists — keeping default");
    }

    private int _selectedTrackIndex = -1;

    /// <summary>The highlighted row. The list box sets it (two-way); the App owns it, so a set is a command and
    /// the cache follows the App's <see cref="SelectionChanged"/>.</summary>
    public int SelectedTrackIndex
    {
        get => _selectedTrackIndex;
        set
        {
            if (_selectedTrackIndex == value) return;
            SetSelectedTrackIndex(value);
            _sender.Send(new SelectTrack(value, false, Ui("library", "select")));
        }
    }

    private void SetSelectedTrackIndex(int value)
    {
        if (_selectedTrackIndex == value) return;
        _selectedTrackIndex = value;
        Notify(nameof(SelectedTrackIndex));
        Notify(nameof(SelectedTrack));
    }

    void IEventHandler<SelectionChanged>.Handle(in SelectionChanged e) => SetSelectedTrackIndex(e.Index);

    public Track? SelectedTrack => SelectedTrackRow?.Track;

    public TrackRow? SelectedTrackRow =>
        SelectedTrackIndex >= 0 && SelectedTrackIndex < Tracks.Count
            ? Tracks[SelectedTrackIndex]
            : null;

    /// <summary>Highlight row <paramref name="index"/>, clamped to the visible rows by the App.</summary>
    public void SelectTrack(int index) => _sender.Send(new SelectTrack(index, true, Ui("library", "select")));

    /// <summary>Label of the active tag or crate filter, or null when the whole library shows.</summary>
    public string? ActiveFilter { get; private set; }

    void IEventHandler<LibraryFilterChanged>.Handle(in LibraryFilterChanged e)
    {
        ActiveFilter = e.Label;
        Notify(nameof(ActiveFilter));
    }

    /// <summary>Show the whole library again (Esc).</summary>
    public void ClearFilter() => _sender.Send(new ClearLibraryFilter(Ui("library", "clear-filter")));

    /// <summary>Double-click on a library row: re-analyze the highlighted track. The headless track loader
    /// does it, the same as for the browse-knob long-press.</summary>
    public void RequestReanalyzeSelected() =>
        _sender.Send(new ReanalyzeSelected(new Origin(InterfaceIds.MainUI, "library", "double-click")));

    /// <summary>Menu: choose a different music folder. The App asks (the folder picker shows) and rescans.</summary>
    public void RequestChangeMusicFolder() =>
        _sender.Send(new ChangeMusicFolder(new Origin(InterfaceIds.MainUI, "menu", "change-music-folder")));

    /// <summary>Menu: choose a different output device. The App asks (the device picker shows) and switches.</summary>
    public void RequestChangeOutputDevice() =>
        _sender.Send(new ChangeOutputDevice(new Origin(InterfaceIds.MainUI, "menu", "change-output-device")));

    public DeckViewModel DeckFor(int deck) => deck == 1 ? Deck2 : Deck1;

    /// <summary>The deck whose tune editor is open (deck 1 first), or null. The arrow keys tune this deck.</summary>
    public DeckViewModel? EditingDeck => Deck1.EditOpen ? Deck1 : Deck2.EditOpen ? Deck2 : null;

    private double _crossfader = 0.5;

    /// <summary>0..1, 0 = full Deck 1, 1 = full Deck 2. A set is a command; the cache follows the App's
    /// <see cref="CrossfaderChanged"/> (the mixer applies equal-power gains to each deck).</summary>
    public double Crossfader
    {
        get => _crossfader;
        set => _sender.Send(new SetCrossfader(value, Ui("crossfader", "move")));
    }

    void IEventHandler<CrossfaderChanged>.Handle(in CrossfaderChanged e)
    {
        _crossfader = e.Position;
        Notify(nameof(Crossfader));
    }

    private Key? _harmonyReferenceKey;

    /// <summary>Camelot key of whichever deck is the harmony anchor — Deck 1 if it
    /// has a loaded key, else Deck 2. Drives row dimming in the library list.</summary>
    public Key? HarmonyReferenceKey => _harmonyReferenceKey;

    void IEventHandler<HarmonyReferenceChanged>.Handle(in HarmonyReferenceChanged e)
    {
        _harmonyReferenceKey = e.Key is { } k ? new Key(k.PitchClass, k.IsMajor) : null;
        Notify(nameof(HarmonyReferenceKey));
    }

    /// <summary>Notifying mirror of the magnet-lock eligibility the App publishes
    /// (<see cref="MagnetEligibilityChanged"/>). XAML binds to this so the centerline magnet glyph can pop in /
    /// out via a style-class transition.</summary>
    public bool IsMagnetEligible => _isMagnetEligible;

    void IEventHandler<MagnetEligibilityChanged>.Handle(in MagnetEligibilityChanged e)
    {
        if (_isMagnetEligible == e.Eligible) return;
        _isMagnetEligible = e.Eligible;
        Notify(nameof(IsMagnetEligible));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
