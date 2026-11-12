using System.ComponentModel;
using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Data;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Settings ▸ Analyse whole library / Cancel library analysis: the menu sends the commands with a
/// menu origin, the App's progress shows as a toast while it runs and a summary toast at the end, and the
/// cancel entry is enabled only while it runs.</summary>
[Collection(MainViewModelCollection.Name)]
public class LibraryAnalysisMenuTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly FakeSender _sender = new();
    private readonly FakeToast _toast = new();
    private readonly MainViewModel _vm;

    public LibraryAnalysisMenuTests()
    {
        AvaloniaTestApp.EnsureStarted();
        var themes = new ThemeStackFactory().Build();
        var appThread = new ImmediateAppThread();
        var recency = new TagRecency();
        var rows = new LibraryRowsViewModel(_bus, new TrackRowFactory(themes.Context));
        var clock = new FakeFrameClock();
        var decks = new DeckViewModelFactory(_bus, _bus, themes.Context, Options.Create(new FeatureOptions { ShowSectionMap = false }),
            Options.Create(new DeckViewOptions()), new NoPeaksFactory(), new DiscBloomFactory(new ManualFrameClock()));
        var deck1 = decks.Create(0);
        var deck2 = decks.Create(1);
        var clocks = new DeckViewModelClockSource(deck1, deck2);
        var glance = new GlanceViewModelFactory(_bus, _bus, _bus, appThread, clock, rows, recency,
            new GlanceHeaderViewModel(clocks, clock, _bus, new DeckSlotFactory(clocks, new FixedMotionPreference(false), Options.Create(new GlanceViewOptions()))),
            new FixedMotionPreference(false), Options.Create(new GlanceViewOptions())).Create();
        var waveformStyle = new WaveformStyleViewModel(new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create(), _bus);
        var themeViewModel = new ThemeViewModel(themes.Context, themes.Catalog, _bus);
        _vm = new MainViewModel(
            themeViewModel, rows, _sender, _bus, glance, new LoadFeedbackViewModel(_bus, _bus), new OutputPickerViewModel(),
            deck1, deck2, waveformStyle,
            new LayoutWizardViewModel(waveformStyle, new WaveformStyleOptionFactory(), new WaveformPreviewRenderer(),
                new DemoWaveformFactory(), new WaveformPreviewScroll(new ManualFrameClock()), appThread,
                themeViewModel, new ThemeOptionFactory()),
            new SettingsViewModel(_bus, _bus, new KnobScaleFactory()),
            new FoldersViewModel(_bus, _bus, _bus, new FolderRowViewModelFactory(),
                Microsoft.Extensions.Options.Options.Create(new FoldersPanelOptions())),
            new TrackListViewModel(_bus, _bus),
            _toast,
            new TrackActionsCoordinator(new OverlayViewModelFactory(recency, _bus, _bus, _bus, appThread), new TrackActionsViewModel()),
            new SystemReportCoordinator(new SystemReportViewModel()),
            new FaceplateGuideCoordinator(new CollapseToIconSequence(new FakeFrameClock(), new FixedMotionPreference(false),
                new CollapseToIconOptions("faceplate", new CollapseToIconTimingsFactory().Standard()), new AlwaysHintPolicy())));
    }

    [Fact]
    public void The_menu_entry_sends_analyse_library_from_the_menu()
    {
        _vm.RequestAnalyzeLibrary();

        var sent = Assert.IsType<AnalyzeLibrary>(Assert.Single(_sender.Sent));
        Assert.Equal(InterfaceIds.MainUI, sent.Origin.InterfaceId);
        Assert.Equal("menu", sent.Origin.ControlId);
    }

    [Fact]
    public void The_cancel_entry_sends_cancel_library_analysis_from_the_menu()
    {
        _vm.RequestCancelLibraryAnalysis();

        var sent = Assert.IsType<CancelLibraryAnalysis>(Assert.Single(_sender.Sent));
        Assert.Equal(InterfaceIds.MainUI, sent.Origin.InterfaceId);
        Assert.Equal("menu", sent.Origin.ControlId);
    }

    [Fact]
    public void Each_progress_event_shows_the_count_as_a_toast_and_the_last_a_summary()
    {
        _bus.Publish(new LibraryAnalysisProgressChanged(0, 340, 0, true));
        _bus.Publish(new LibraryAnalysisProgressChanged(12, 340, 0, true));
        _bus.Publish(new LibraryAnalysisProgressChanged(338, 340, 2, false));

        Assert.Equal(
            ["Analysing library 0 of 340", "Analysing library 12 of 340", "Library analysis finished · 338 of 340 analysed · 2 failed"],
            _toast.Shown);
    }

    [Fact]
    public void A_cancelled_run_reads_stopped_in_its_summary()
    {
        _bus.Publish(new LibraryAnalysisProgressChanged(12, 340, 0, false));

        Assert.Equal("Library analysis stopped · 12 of 340 analysed", _toast.Shown[^1]);
    }

    [Fact]
    public void Cancel_is_enabled_only_while_a_library_analysis_runs()
    {
        Assert.False(_vm.IsLibraryAnalysisRunning);

        _bus.Publish(new LibraryAnalysisProgressChanged(0, 3, 0, true));
        Assert.True(_vm.IsLibraryAnalysisRunning);

        _bus.Publish(new LibraryAnalysisProgressChanged(3, 3, 0, false));
        Assert.False(_vm.IsLibraryAnalysisRunning);
    }

    private sealed class FakeSender : ICommandSender
    {
        public List<ICommand> Sent { get; } = [];

        public void Send<T>(in T command) where T : struct, ICommand => Sent.Add(command);
    }

    private sealed class FakeToast : IToastViewModel
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public List<string> Shown { get; } = [];

        public string? Toast => Shown.Count == 0 ? null : Shown[^1];

        public bool HasToast => Shown.Count > 0;

        public void Show(string text)
        {
            Shown.Add(text);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Toast)));
        }
    }
}
