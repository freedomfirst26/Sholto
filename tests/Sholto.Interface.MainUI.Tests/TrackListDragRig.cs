using Microsoft.Extensions.Options;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.Faceplate;
using Sholto.Interface.Faceplate.Devices.DdjFlx4;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.Views;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The real App Track List over a real library session, projected into the real main view model and
/// shown in the real main window: [Alpha, Bravo, Charlie] loaded as the Track List. Commands the view model
/// sends are recorded and still reach the App.</summary>
internal sealed class TrackListDragRig
{
    public static readonly string A = LibrarySessionRig.Alpha.FilePath;
    public static readonly string B = LibrarySessionRig.Bravo.FilePath;
    public static readonly string C = LibrarySessionRig.Charlie.FilePath;

    private TrackListDragRig(LibrarySessionRig library, RecordingCommandSender sender, ITrackList list,
        MainViewModel vm, MainWindow window)
    {
        Library = library;
        Sender = sender;
        List = list;
        Vm = vm;
        Window = window;
    }

    public LibrarySessionRig Library { get; }
    public RecordingCommandSender Sender { get; }
    public ITrackList List { get; }
    public MainViewModel Vm { get; }
    public MainWindow Window { get; }

    public string[] ListPaths => List.Entries.Select(e => e.Path).ToArray();
    public string[] RowPaths => Vm.Tracks.Select(r => r.FilePath).ToArray();

    public static async Task<TrackListDragRig> CreateAsync(bool reducedMotion = false)
    {
        AvaloniaTestApp.EnsureStarted();
        var library = new LibrarySessionRig();
        var bus = library.Bus;
        var appThread = new ImmediateAppThread();
        var sender = new RecordingCommandSender(bus);
        var themes = new ThemeStackFactory().Build();
        var recency = new TagRecency();
        var rows = new LibraryRowsViewModel(bus, new TrackRowFactory(themes.Context));
        var clock = new FakeFrameClock();
        var deckViewModels = new DeckViewModelFactory(bus, bus, themes.Context, Options.Create(new FeatureOptions()), Options.Create(new DeckViewOptions()),
            new NoPeaksFactory(), new DiscBloomFactory(new ManualFrameClock()));
        var deck1 = deckViewModels.Create(0);
        var deck2 = deckViewModels.Create(1);
        var clocks = new DeckViewModelClockSource(deck1, deck2);
        var glance = new GlanceViewModel(bus, bus, bus, appThread, clock, rows, recency,
            new GlanceHeaderViewModel(clocks, clock, bus, new DeckSlotFactory(clocks, new FixedMotionPreference(reducedMotion), Options.Create(new GlanceViewOptions()))),
            new FixedMotionPreference(reducedMotion), Options.Create(new GlanceViewOptions()));
        var waveformStyle = new WaveformStyleViewModel(new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create(), bus);
        var themeViewModel = new ThemeViewModel(themes.Context, themes.Catalog, bus);
        var vm = new MainViewModel(
            themeViewModel,
            new OverlayViewModelFactory(recency, bus, bus, bus, appThread),
            rows,
            bus,
            bus,
            appThread,
            glance,
            new LoadFeedbackViewModel(bus, bus),
            new TrackActionsViewModel(),
            new OutputPickerViewModel(),
            deck1,
            deck2,
            waveformStyle,
            new LayoutWizardViewModel(waveformStyle, new WaveformStyleOptionFactory(), new WaveformPreviewRenderer(),
                new DemoWaveformFactory(), new WaveformPreviewScroll(new ManualFrameClock()), appThread,
                themeViewModel, new ThemeOptionFactory()),
            new SettingsViewModel(bus, bus, new KnobScaleFactory()),
            new SystemReportViewModel(),
            new CollapseToIconSequence(new FakeFrameClock(), new FixedMotionPreference(false),
                new CollapseToIconOptions("faceplate", new CollapseToIconTimingsFactory().Standard()),
                new AlwaysHintPolicy()),
            new TrackListViewModel(sender, bus));

        // The App side: the library scanned, then the real Track List holding the three songs in order.
        await library.Library.ScanAsync("/music", library.Stack());
        var list = new TrackList(library.Library, appThread, bus);
        bus.Register<MoveInTrackList>(list);
        var origin = new Origin(InterfaceIds.MainUI, "test", "load");
        foreach (var path in new[] { A, B, C }) list.Handle(new LoadSongToTrackList(path, origin));

        var overlay = new FaceplateOverlayFactory(new FaceplateDocLoader(), bus, bus)
            .Create(new DdjFlx4Faceplate());
        var window = new MainWindow(overlay, themes.Catalog, new ModalKeyRouter(), new AppIconFactory()) { DataContext = vm };
        return new TrackListDragRig(library, sender, list, vm, window);
    }
}
