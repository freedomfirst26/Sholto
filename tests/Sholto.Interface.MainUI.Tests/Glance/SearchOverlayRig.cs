using Microsoft.Extensions.Options;
using Avalonia.Controls;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The Glance overlay shown in a headless window over a real <see cref="MainViewModel"/> whose Glance is the
/// <see cref="GlanceRig"/>'s, so a click on the overlay can be read off the commands the rig logs.</summary>
internal sealed class SearchOverlayRig
{
    public GlanceRig Rig { get; } = new();
    public MainViewModel Vm { get; }
    public SearchOverlay Overlay { get; } = new();

    public SearchOverlayRig()
    {
        var bus = Rig.Bus;
        var themes = new ThemeStackFactory().Build();
        var appThread = new ImmediateAppThread();
        var deck1 = new DeckViewModel(0, bus, bus, themes.Context, new NoPeaksFactory(), new DiscBloomFactory(new ManualFrameClock()));
        var deck2 = new DeckViewModel(1, bus, bus, themes.Context, new NoPeaksFactory(), new DiscBloomFactory(new ManualFrameClock()));
        var waveformStyle = new WaveformStyleViewModel(new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create(), bus);
        var themeViewModel = new ThemeViewModel(themes.Context, themes.Catalog, bus);
        Vm = new MainViewModel(
            themeViewModel,
            new OverlayViewModelFactory(new TagRecency(), bus, bus, bus, appThread),
            Rig.Library,
            bus,
            bus,
            appThread,
            Rig.Glance,
            new LoadFeedbackViewModel(bus, bus),
            new TrackActionsViewModel(),
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
            new TrackListViewModel(bus, bus));
        // The overlay draws from the theme's brushes, which the real window publishes to its resources.
        var window = new Window { Width = 1100, Height = 760 };
        new ThemeResourcesApplier().Apply(window.Resources, themes.Catalog.All[0]);
        window.Content = Overlay;
        Overlay.DataContext = Vm;
        window.Show();
    }
}
