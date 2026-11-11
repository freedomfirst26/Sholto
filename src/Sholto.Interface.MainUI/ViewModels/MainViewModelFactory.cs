using Sholto.Data;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Holds the main view model's dependencies and builds it: <see cref="Create"/> makes the
/// toast, track-actions, system-report and faceplate-guide collaborators and hands them in through the
/// constructor.</summary>
public sealed class MainViewModelFactory(
    IThemeViewModel themes, IOverlayViewModelFactory overlays, LibraryRowsViewModel rows, ICommandSender sender,
    IEventSubscriber subscriber, IAppThread appThread, IGlanceViewModel glance, ILoadFeedbackViewModel loadFeedback,
    TrackActionsViewModel trackActions, OutputPickerViewModel outputPicker, DeckViewModel deck1, DeckViewModel deck2,
    IWaveformStyleViewModel waveformStyle, ILayoutWizardViewModel layoutWizard, ISettingsViewModel settings,
    ISystemReportViewModel systemReport, ICollapseToIconSequence faceplateDock, ITrackListViewModel trackList)
    : IMainViewModelFactory
{
    private readonly IThemeViewModel _themes = themes;
    private readonly IOverlayViewModelFactory _overlays = overlays;
    private readonly LibraryRowsViewModel _rows = rows;
    private readonly ICommandSender _sender = sender;
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly IAppThread _appThread = appThread;
    private readonly IGlanceViewModel _glance = glance;
    private readonly ILoadFeedbackViewModel _loadFeedback = loadFeedback;
    private readonly TrackActionsViewModel _trackActions = trackActions;
    private readonly OutputPickerViewModel _outputPicker = outputPicker;
    private readonly DeckViewModel _deck1 = deck1;
    private readonly DeckViewModel _deck2 = deck2;
    private readonly IWaveformStyleViewModel _waveformStyle = waveformStyle;
    private readonly ILayoutWizardViewModel _layoutWizard = layoutWizard;
    private readonly ISettingsViewModel _settings = settings;
    private readonly ISystemReportViewModel _systemReport = systemReport;
    private readonly ICollapseToIconSequence _faceplateDock = faceplateDock;
    private readonly ITrackListViewModel _trackList = trackList;

    /// <summary>Call once: the handed-in view models are wired by each call.</summary>
    public MainViewModel Create() => new(
        _themes, _overlays, _rows, _sender, _subscriber, _appThread, _glance, _loadFeedback, _trackActions,
        _outputPicker, _deck1, _deck2, _waveformStyle, _layoutWizard, _settings, _systemReport, _faceplateDock,
        _trackList);
}
