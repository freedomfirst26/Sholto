using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The main window's view model: overlays, the Track List strip, the load target
/// choice, the faceplate mount, the theme, the toast and the system report. It talks to the App only
/// through the bus: it subscribes to the library, mixer, controller and toast-worthy events, caches what it
/// shows, and sends commands for every action. Events arrive on the app thread (the UI thread), so the
/// handlers update directly.</summary>
public sealed class MainViewModel :
    INotifyPropertyChanged,
    IEventHandler<SelectionChanged>,
    IEventHandler<LibraryUnreachableChanged>,
    IEventHandler<HarmonyReferenceChanged>,
    IEventHandler<LibraryDatabaseAttached>,
    IEventHandler<CrossfaderChanged>,
    IEventHandler<MagnetEligibilityChanged>,
    IEventHandler<DeviceConnectionChanged>,
    IEventHandler<MarkerAdded>,
    IEventHandler<TrackAddedToCrate>,
    IEventHandler<TrackLoadFailed>,
    IEventHandler<SystemCheckReported>
{
    private bool _isMagnetEligible;

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly LibraryRowsViewModel _rows;

    /// <summary>The visible library rows; the library list and the search overlay bind to it.</summary>
    public ObservableCollection<TrackRow> Tracks => _rows.Items;

    /// <summary>The Track List strip above the library list, and its reorder drag.</summary>
    public ITrackListViewModel TrackList { get; }

    /// <summary>The Glance search overlay (Space).</summary>
    public IGlanceViewModel Glance { get; }

    /// <summary>The replace-a-playing-deck warning, the undo toast and the undo confirmation.</summary>
    public ILoadFeedbackViewModel LoadFeedback { get; }

    /// <summary>The overlay is open. Setting it opens or closes <see cref="Glance"/>.</summary>
    public bool IsSearchOpen
    {
        get => Glance.IsOpen;
        set
        {
            if (value) Glance.Open();
            else Glance.Close();
        }
    }

    /// <summary>The load warning on the main window: shown only while the overlay is closed, since the
    /// overlay carries its own copy.</summary>
    public bool ShowLoadWarning => LoadFeedback.HasWarning && !Glance.IsOpen;

    /// <summary>Load the currently-selected library row into the given deck. Used by Enter in the search
    /// overlay AND by the 1 / 2 hotkeys / FLX-4 LOAD buttons (which send the same command): the headless
    /// track loader does the work.</summary>
    public void LoadSelectedToDeck(int deckIndex) =>
        _sender.Send(new LoadSelectedIntoDeck(deckIndex, new Origin(InterfaceIds.MainUI, "search", "load", deckIndex)));

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
        CratePicker.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CratePickerViewModel.IsOpen)) Notify(nameof(IsCratePickerOpen));
        };
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
    public CratePickerViewModel? CratePicker { get; private set; }

    private bool _isTrackActionsOpen;
    public bool IsTrackActionsOpen
    {
        get => _isTrackActionsOpen;
        private set { if (_isTrackActionsOpen == value) return; _isTrackActionsOpen = value; Notify(); }
    }

    /// <summary>Whether the crate picker is showing (a pass-through to <see cref="CratePicker"/>).</summary>
    public bool IsCratePickerOpen => CratePicker?.IsOpen ?? false;

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

    /// <summary>When the controller guide is open, collapsing into its top-bar icon, hinting or idle. The
    /// view reads it to animate; <see cref="Mount"/> drives it.</summary>
    public ICollapseToIconSequence FaceplateDock { get; }

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
            // Inspect is already off and the selection gone: only now does the guide start shrinking into its icon.
            if (isOpen) FaceplateDock.Open();
            else FaceplateDock.Collapse();
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
    /// <summary>The Track List holds no songs AND the main list has no rows to show: the empty state replaces
    /// the list. Rows with an empty Track List (before the first restore) never show it, so the screen is never
    /// half empty and half full.</summary>
    public bool ShowTrackListEmpty => TrackList.IsEmpty && Tracks.Count == 0;

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

    private SystemHealth? _systemHealth;

    /// <summary>The system report modal (the amber status dot): one row per external tool and a headline.</summary>
    public ISystemReportViewModel SystemReportModal { get; }

    /// <summary>The boot-time tool probe's result, published once at startup by the App side. Nothing here
    /// re-probes the filesystem, and availability deliberately never goes live (see ExternalToolFinder: a
    /// tool installed mid-session needs a restart).</summary>
    void IEventHandler<SystemCheckReported>.Handle(in SystemCheckReported e)
    {
        _systemHealth = e.Health;
        SystemReportModal.Report(e);
        Notify(nameof(SystemDegraded));
    }

    /// <summary>Amber dot: some tool is missing AND the controller is fine. Never true
    /// while the controller is down — red wins, and the dot only ever shows one
    /// colour. Both <c>Degraded</c> and <c>Offline</c> land here; the severity shows
    /// up in the report's headline, not in the colour.</summary>
    public bool SystemDegraded =>
        _controllerConnected && _systemHealth is { } h && h != SystemHealth.Healthy;

    /// <summary>Whether the system report is showing (a pass-through to <see cref="SystemReportModal"/>).
    /// The only way in is a click on the amber dot.</summary>
    public bool IsSystemReportOpen => SystemReportModal.IsOpen;

    /// <summary>Click handler's entry point. No-ops unless the dot is actually amber,
    /// so a green (or red) dot is not a hidden button.</summary>
    public void OpenSystemReport()
    {
        if (!SystemDegraded) return;
        SystemReportModal.Open();
    }

    public void CloseSystemReport() => SystemReportModal.Close();

    /// <summary>Which waveform style the decks draw (and the Layout Wizard's choice).</summary>
    public IWaveformStyleViewModel WaveformStyle { get; }

    /// <summary>The Layout Wizard overlay (Settings ▸ Layout Wizard…).</summary>
    public ILayoutWizardViewModel LayoutWizard { get; }

    /// <summary>Open the Layout Wizard. Its preview cards play a demo track in the current theme's colours.</summary>
    public void OpenLayoutWizard() => LayoutWizard.Open(Theme.Waveform);

    /// <summary>The Settings overlay (Settings ▸ Settings…): the backspin release knob.</summary>
    public ISettingsViewModel Settings { get; }

    /// <summary>The shell modals in key priority order (first open one gets the key): crate picker, system report,
    /// layout wizard, settings. The crate picker is built lazily with the library, so it is skipped until then.
    /// Built on each read.</summary>
    public IReadOnlyList<IModal> Modals
    {
        get
        {
            var modals = new List<IModal>(4);
            if (CratePicker is not null) modals.Add(CratePicker);
            modals.Add(SystemReportModal);
            modals.Add(LayoutWizard);
            modals.Add(Settings);
            return modals;
        }
    }

    public void OpenSettings() => Settings.Open();

    private readonly IOverlayViewModelFactory _overlays;
    private readonly IThemeViewModel _themes;
    private readonly ICommandSender _sender;
    private readonly IAppThread _appThread;

    public MainViewModel(IThemeViewModel themes,
                         IOverlayViewModelFactory overlays,
                         LibraryRowsViewModel rows,
                         ICommandSender sender,
                         IEventSubscriber subscriber,
                         IAppThread appThread,
                         IGlanceViewModel glance,
                         ILoadFeedbackViewModel loadFeedback,
                         TrackActionsViewModel trackActions,
                         DeckViewModel deck1,
                         DeckViewModel deck2,
                         IWaveformStyleViewModel waveformStyle,
                         ILayoutWizardViewModel layoutWizard,
                         ISettingsViewModel settings,
                         ISystemReportViewModel systemReport,
                         ICollapseToIconSequence faceplateDock,
                         ITrackListViewModel trackList)
    {
        TrackList = trackList;
        TrackList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ITrackListViewModel.IsEmpty)) Notify(nameof(ShowTrackListEmpty));
        };
        rows.Items.CollectionChanged += (_, _) => Notify(nameof(ShowTrackListEmpty));
        SystemReportModal = systemReport;
        SystemReportModal.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ISystemReportViewModel.IsOpen)) Notify(nameof(IsSystemReportOpen));
        };
        FaceplateDock = faceplateDock;
        WaveformStyle = waveformStyle;
        LayoutWizard = layoutWizard;
        Settings = settings;
        _themes = themes;
        _overlays = overlays;
        _sender = sender;
        _appThread = appThread;
        _rows = rows;
        TrackActions = trackActions;

        // A theme worn for any reason (menu pick, restore, the wizard's live try-on) re-emits the theme-derived
        // bindings; only a choice is sent to the App.
        _themes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IThemeViewModel.Shown)) OnThemeShown();
        };

        Glance = glance;
        LoadFeedback = loadFeedback;
        Glance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(IGlanceViewModel.IsOpen)) return;
            Notify(nameof(IsSearchOpen));
            Notify(nameof(ShowLoadWarning));
        };
        LoadFeedback.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ILoadFeedbackViewModel.HasWarning)) Notify(nameof(ShowLoadWarning));
        };
        WireTrackActions();

        Deck1 = deck1;
        Deck2 = deck2;

        // The App announces; this view model caches and re-emits for the bindings. State events are
        // replayed on subscribe, so a view model built after the App started still shows the real picture.
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
        subscriber.Subscribe<SystemCheckReported>(this);
    }

    private Origin Ui(string control, string gesture) => new(InterfaceIds.MainUI, control, gesture);

    /// <summary>The theme the app wears now. Setting it is the user's choice (saved by the App).</summary>
    public SholtoTheme Theme
    {
        get => _themes.Shown;
        set => _themes.Choose(value);
    }

    private void OnThemeShown()
    {
        Notify(nameof(Theme));
        // Re-emit theme-derived bindings on each track and deck so KeyBrush
        // re-evaluates against the new palette. Cheaper than a static event
        // subscription (which would pin every TrackRow until app exit).
        _rows.RefreshThemeBindings();
        Deck1.RefreshThemeBindings();
        Deck2.RefreshThemeBindings();
    }

    /// <summary>Apply the theme saved last time, if the catalog still has one of that name. Not a user
    /// choice: the App already has the name.</summary>
    public void RestoreTheme(string savedName) => _themes.Restore(savedName);

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

    public TrackSummary? SelectedTrack => SelectedTrackRow?.Summary;

    public TrackRow? SelectedTrackRow =>
        SelectedTrackIndex >= 0 && SelectedTrackIndex < Tracks.Count
            ? Tracks[SelectedTrackIndex]
            : null;

    /// <summary>Highlight row <paramref name="index"/>, clamped to the visible rows by the App.</summary>
    public void SelectTrack(int index) => _sender.Send(new SelectTrack(index, true, Ui("library", "select")));

    /// <summary>Esc on the main view: closes an open deck tuner (true), else does nothing (false).</summary>
    public bool HandleEscape()
    {
        if (Deck1.EditOpen || Deck2.EditOpen)
        {
            Deck1.CloseEditor();
            Deck2.CloseEditor();
            return true;
        }
        return false;
    }

    /// <summary>Delete on the main view: take the highlighted song out of the Track List.</summary>
    public void RemoveHighlighted() => TrackList.RemoveSong(SelectedTrackRow?.FilePath);

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

    private KeyRef? _harmonyReferenceKey;

    /// <summary>Camelot key of whichever deck is the harmony anchor — Deck 1 if it
    /// has a loaded key, else Deck 2. Drives row dimming in the library list.</summary>
    public KeyRef? HarmonyReferenceKey => _harmonyReferenceKey;

    void IEventHandler<HarmonyReferenceChanged>.Handle(in HarmonyReferenceChanged e)
    {
        _harmonyReferenceKey = e.Key;
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
