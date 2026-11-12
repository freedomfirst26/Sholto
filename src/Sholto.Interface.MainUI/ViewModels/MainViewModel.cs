using System.Collections.ObjectModel;
using System.ComponentModel;
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
    IEventHandler<SystemCheckReported>,
    IEventHandler<LibraryAnalysisProgressChanged>
{
    private bool _isMagnetEligible;

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ILibraryRowsViewModel _rows;

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

    public TagEditorViewModel? TagEditor => _trackActions.TagEditor;
    public bool IsTagEditorOpen => _trackActions.IsTagEditorOpen;

    /// <summary>The database is up (the App announces it): the track actions build the tag editor and crate picker.</summary>
    void IEventHandler<LibraryDatabaseAttached>.Handle(in LibraryDatabaseAttached e) => _trackActions.AttachLibrary(e.Available);

    public Task OpenTagEditorAsync(TrackRow row) => _trackActions.OpenTagEditorAsync(row);

    // ---- Enter-mode: the track action menu + crate picker ----------------------

    public TrackActionsViewModel TrackActions => _trackActions.TrackActions;
    public IOutputPickerViewModel OutputPicker { get; }

    private bool _isOutputPickerOpen;
    public bool IsOutputPickerOpen
    {
        get => _isOutputPickerOpen;
        private set { if (_isOutputPickerOpen == value) return; _isOutputPickerOpen = value; Notify(); }
    }
    public CratePickerViewModel? CratePicker => _trackActions.CratePicker;
    public bool IsTrackActionsOpen => _trackActions.IsTrackActionsOpen;

    /// <summary>Whether the crate picker is showing (a pass-through to <see cref="CratePicker"/>).</summary>
    public bool IsCratePickerOpen => _trackActions.IsCratePickerOpen;

    // ---- Controller guide (Faceplate) -------------------------------------------

    /// <summary>The controller guide's own state; null until <see cref="AttachFaceplate"/> runs.</summary>
    public Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel? Faceplate => _faceplate.Faceplate;

    /// <summary>Hand in the guide's own view model once the overlay hosting it exists.</summary>
    public void AttachFaceplate(Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel faceplate) =>
        _faceplate.AttachFaceplate(faceplate);

    /// <summary>When the controller guide is open, collapsing into its top-bar icon, hinting or idle.</summary>
    public ICollapseToIconSequence FaceplateDock => _faceplate.FaceplateDock;

    /// <summary>Where the controller guide is shown.</summary>
    public Sholto.Interface.Faceplate.FaceplateMount Mount
    {
        get => _faceplate.Mount;
        set => _faceplate.Mount = value;
    }

    /// <summary>Whether the controller guide overlay is showing; a bool view over <see cref="Mount"/>.</summary>
    public bool IsFaceplateOpen
    {
        get => _faceplate.IsFaceplateOpen;
        set => _faceplate.IsFaceplateOpen = value;
    }

    /// <summary>Brief confirmation text (e.g. "Added to Warmup Set"); null = hidden.</summary>
    public string? Toast => _toast.Toast;
    public bool HasToast => _toast.HasToast;

    private void WireOutputPicker()
    {
        OutputPicker.Opened += () => IsOutputPickerOpen = true;
        OutputPicker.RequestClose += () => IsOutputPickerOpen = false;
    }

    /// <summary>Enter on a library row opens the action menu for it.</summary>
    public void OpenTrackActions(TrackRow row) => _trackActions.OpenTrackActions(row);

    public Task OpenCratePickerAsync(TrackRow row) => _trackActions.OpenCratePickerAsync(row);

    /// <summary>A marker was dropped on a deck (M key; Shift = Deck 2): confirm it with a toast.</summary>
    void IEventHandler<MarkerAdded>.Handle(in MarkerAdded e) =>
        _toast.Show($"Marker · Deck {e.Deck + 1} · {TimeSpan.FromSeconds(e.Seconds):m\\:ss}");

    /// <summary>A track went into a crate (from the crate picker): confirm it with a toast.</summary>
    void IEventHandler<TrackAddedToCrate>.Handle(in TrackAddedToCrate e) => _toast.Show($"Added to {e.CrateName}");

    /// <summary>A track could not be loaded: the deck stays usable; say so.</summary>
    void IEventHandler<TrackLoadFailed>.Handle(in TrackLoadFailed e) =>
        _toast.Show($"Couldn't load {e.Title} · Deck {e.Deck + 1}");

    private bool _isLibraryAnalysisRunning;
    /// <summary>A library analysis is running: the menu's cancel entry is enabled only then.</summary>
    public bool IsLibraryAnalysisRunning
    {
        get => _isLibraryAnalysisRunning;
        private set
        {
            if (_isLibraryAnalysisRunning == value) return;
            _isLibraryAnalysisRunning = value;
            Notify();
        }
    }

    /// <summary>The library analysis moved on: its progress as a toast while it runs, a summary at the end.</summary>
    void IEventHandler<LibraryAnalysisProgressChanged>.Handle(in LibraryAnalysisProgressChanged e)
    {
        IsLibraryAnalysisRunning = e.Running;
        _toast.Show(e.Running ? $"Analysing library {e.Done} of {e.Total}" : LibraryAnalysisSummary(e));
    }

    private string LibraryAnalysisSummary(in LibraryAnalysisProgressChanged e)
    {
        if (e.Total == 0) return "Library analysis: nothing to analyse";
        var failed = e.Failed > 0 ? $" · {e.Failed} failed" : "";
        var verb = e.Done + e.Failed < e.Total ? "stopped" : "finished";
        return $"Library analysis {verb} · {e.Done} of {e.Total} analysed{failed}";
    }

    public IDeckViewModel Deck1 { get; }
    public IDeckViewModel Deck2 { get; }

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

    /// <summary>The system report modal (the amber status dot): one row per external tool and a headline.</summary>
    public ISystemReportViewModel SystemReportModal => _systemReport.SystemReportModal;

    /// <summary>The boot-time tool probe's result, published once at startup by the App side. Nothing here
    /// re-probes the filesystem, and availability deliberately never goes live (see ExternalToolFinder: a
    /// tool installed mid-session needs a restart).</summary>
    void IEventHandler<SystemCheckReported>.Handle(in SystemCheckReported e)
    {
        _systemReport.Report(e);
        Notify(nameof(SystemDegraded));
    }

    /// <summary>Amber dot: some tool is missing AND the controller is fine. Never true
    /// while the controller is down — red wins, and the dot only ever shows one
    /// colour. Both <c>Degraded</c> and <c>Offline</c> land here; the severity shows
    /// up in the report's headline, not in the colour.</summary>
    public bool SystemDegraded =>
        _controllerConnected && _systemReport.ToolsMissing;

    /// <summary>Whether the system report is showing (a pass-through to <see cref="SystemReportModal"/>).
    /// The only way in is a click on the amber dot.</summary>
    public bool IsSystemReportOpen => _systemReport.IsSystemReportOpen;

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

    /// <summary>The Folders panel (Settings ▸ Folders…, first run, upgrade, drive missing): the music and analysis folders.</summary>
    public IFoldersViewModel Folders { get; }

    /// <summary>The shell modals in key priority order (first open one gets the key): crate picker, system report,
    /// layout wizard, settings, folders. The crate picker is built lazily with the library, so it is skipped until then.
    /// Built on each read.</summary>
    public IReadOnlyList<IModal> Modals
    {
        get
        {
            var modals = new List<IModal>(5);
            if (CratePicker is not null) modals.Add(CratePicker);
            modals.Add(SystemReportModal);
            modals.Add(LayoutWizard);
            modals.Add(Settings);
            modals.Add(Folders);
            return modals;
        }
    }

    public void OpenSettings() => Settings.Open();

    private readonly IThemeViewModel _themes;
    private readonly ICommandSender _sender;
    private readonly IToastViewModel _toast;
    private readonly ITrackActionsCoordinator _trackActions;
    private readonly ISystemReportCoordinator _systemReport;
    private readonly IFaceplateGuideCoordinator _faceplate;

    public MainViewModel(IThemeViewModel themes,
                         ILibraryRowsViewModel rows,
                         ICommandSender sender,
                         IEventSubscriber subscriber,
                         IGlanceViewModel glance,
                         ILoadFeedbackViewModel loadFeedback,
                         IOutputPickerViewModel outputPicker,
                         IDeckViewModel deck1,
                         IDeckViewModel deck2,
                         IWaveformStyleViewModel waveformStyle,
                         ILayoutWizardViewModel layoutWizard,
                         ISettingsViewModel settings,
                         IFoldersViewModel folders,
                         ITrackListViewModel trackList,
                         IToastViewModel toast,
                         ITrackActionsCoordinator trackActions,
                         ISystemReportCoordinator systemReport,
                         IFaceplateGuideCoordinator faceplate)
    {
        TrackList = trackList;
        TrackList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ITrackListViewModel.IsEmpty)) Notify(nameof(ShowTrackListEmpty));
        };
        rows.Items.CollectionChanged += (_, _) => Notify(nameof(ShowTrackListEmpty));
        WaveformStyle = waveformStyle;
        LayoutWizard = layoutWizard;
        Settings = settings;
        Folders = folders;
        _themes = themes;
        _sender = sender;
        _rows = rows;
        OutputPicker = outputPicker;
        _toast = toast;
        _trackActions = trackActions;
        _systemReport = systemReport;
        _faceplate = faceplate;
        // The collaborators raise only names this view model has; relay them before the first Subscribe,
        // since state events replay on subscribe.
        _toast.PropertyChanged += (_, e) => Notify(e.PropertyName);
        _trackActions.PropertyChanged += (_, e) => Notify(e.PropertyName);
        _systemReport.PropertyChanged += (_, e) => Notify(e.PropertyName);
        _faceplate.PropertyChanged += (_, e) => Notify(e.PropertyName);

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
        WireOutputPicker();

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
        subscriber.Subscribe<LibraryAnalysisProgressChanged>(this);
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

    /// <summary>Menu or the unreachable-library banner: change the music and analysis folders. The App asks (the
    /// Folders panel shows) and applies the answer. Refused while the output picker is open: the two questions are
    /// never open together.</summary>
    public void RequestChangeFolders()
    {
        if (IsOutputPickerOpen) return;
        _sender.Send(new ChangeFolders(new Origin(InterfaceIds.MainUI, "menu", "change-folders")));
    }

    /// <summary>Menu: analyse every library song not analysed yet, in the background.</summary>
    public void RequestAnalyzeLibrary() =>
        _sender.Send(new AnalyzeLibrary(new Origin(InterfaceIds.MainUI, "menu", "analyze-library")));

    /// <summary>Menu: stop the running library analysis.</summary>
    public void RequestCancelLibraryAnalysis() =>
        _sender.Send(new CancelLibraryAnalysis(new Origin(InterfaceIds.MainUI, "menu", "cancel-library-analysis")));

    /// <summary>Menu: choose a different output device. The App asks (the device picker shows) and switches.
    /// Refused while the Folders panel is open: the two questions are never open together.</summary>
    public void RequestChangeOutputDevice()
    {
        if (Folders.IsOpen) return;
        _sender.Send(new ChangeOutputDevice(new Origin(InterfaceIds.MainUI, "menu", "change-output-device")));
    }

    public IDeckViewModel DeckFor(int deck) => deck == 1 ? Deck2 : Deck1;

    /// <summary>The deck whose tune editor is open (deck 1 first), or null. The arrow keys tune this deck.</summary>
    public IDeckViewModel? EditingDeck => Deck1.EditOpen ? Deck1 : Deck2.EditOpen ? Deck2 : null;

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
