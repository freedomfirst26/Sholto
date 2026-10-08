using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Host;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Faceplate;
using Sholto.Interface.Faceplate.Devices.DdjFlx4;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.Views;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>
/// Builds a real <see cref="MainViewModel"/> + <see cref="MainWindow"/> pair the
/// same way <c>App.OnFrameworkInitializationCompleted</c> does, over the headless core
/// <see cref="IHeadlessCoreFactory"/> builds (the same no-op decode/segment/stem fakes as the
/// headless bench, no DB, no audio device, no MIDI controller behind it).
/// </summary>
/// <param name="headlessApp">Starts Avalonia before anything that needs the asset loader.</param>
/// <param name="coreFactory">Builds the headless core and its two decks.</param>
/// <param name="sender">The data bus the guide and view models send commands on.</param>
/// <param name="asker">The data bus the view models ask their queries on.</param>
/// <param name="subscriber">The data bus the view models follow events on.</param>
/// <param name="options">The configured options the view models are built with.</param>
/// <param name="appThread">The app thread the view models marshal onto.</param>
public sealed class BenchAppFactory(IBenchHeadlessApp headlessApp, IHeadlessCoreFactory coreFactory,
    ICommandSender sender, IQueryAsker asker, IEventSubscriber subscriber, SholtoOptions options,
    IAppThread appThread) : IBenchAppFactory
{
    public BenchApp Create()
    {
        headlessApp.EnsureStarted();

        var headless = coreFactory.Build();
        // Themes load through Avalonia's AssetLoader, so the stack is built after
        // EnsureStarted (above), via the same factory App uses.
        var themeStack = new ThemeStackFactory().Build();
        var themeCatalog = themeStack.Catalog;
        var themeContext = themeStack.Context;

        var tagRecency = new Sholto.Interface.MainUI.Models.TagRecency();
        var rows = new LibraryRowsViewModel(subscriber, new TrackRowFactory(themeContext));
        var deckViewModels = new DeckViewModelFactory(
            subscriber, sender, themeContext, options.Feature, options.DeckView, new NoPeaksFactory(),
            new DiscBloomFactory(new ManualFrameClock()));
        var deck1ViewModel = deckViewModels.Create(0);
        var deck2ViewModel = deckViewModels.Create(1);
        // Glance re-ranks and counts down on this clock; the scenario driver ticks it while a step settles.
        var uiClock = new ManualFrameClock();
        var deckClocks = new DeckViewModelClockSource(deck1ViewModel, deck2ViewModel);
        var motion = new DesktopMotionPreference(Environment.GetEnvironmentVariable, new GsettingsAnimationSetting());
        // The rail's crates and tags come from the demo database (a "database" step fills it), and
        // SHOLTO_HARNESS_SLOWRANK (ms) holds each ranking back so the slow indicator can be shot.
        _ = int.TryParse(Environment.GetEnvironmentVariable("SHOLTO_HARNESS_SLOWRANK"), out var rankDelayMs);
        var glance = new GlanceViewModel(
            sender, new DemoGlanceAsker(asker, rankDelayMs), subscriber, appThread, uiClock, rows, tagRecency,
            new GlanceHeaderViewModel(deckClocks, uiClock, subscriber, new DeckSlotFactory(deckClocks, motion, options.GlanceView)), motion, options.GlanceView);
        var waveformStyle = new WaveformStyleViewModel(new WaveformStylesFactory(options.WaveformStyle).Create(), sender);
        var themeViewModel = new ThemeViewModel(themeContext, themeCatalog, sender);
        var vm = new MainViewModel(
            themeViewModel,
            new OverlayViewModelFactory(tagRecency, asker, sender, subscriber, appThread),
            rows,
            sender,
            subscriber,
            appThread,
            glance,
            new LoadFeedbackViewModel(sender, subscriber),
            new TrackActionsViewModel(),
            deck1ViewModel,
            deck2ViewModel,
            waveformStyle,
            new LayoutWizardViewModel(waveformStyle, new WaveformStyleOptionFactory(), new WaveformPreviewRenderer(),
                new DemoWaveformFactory(), new WaveformPreviewScroll(new ManualFrameClock()), appThread,
                themeViewModel, new ThemeOptionFactory()),
            new SettingsViewModel(subscriber, sender, new KnobScaleFactory()),
            new SystemReportViewModel(),
            new CollapseToIconSequenceFactory(uiClock, motion)
                .Create(new CollapseToIconOptions("faceplate", Slowed(new CollapseToIconTimingsFactory().Standard())),
                    new HintPolicyFactory(asker, sender).Always()),
            new TrackListViewModel(sender, subscriber));

        var device = new DdjFlx4Faceplate();
        var overlay = new FaceplateOverlayFactory(new FaceplateDocLoader(), sender, subscriber).Create(device);
        var window = new MainWindow(overlay, themeCatalog, new ModalKeyRouter()) { DataContext = vm };
        return new BenchApp(vm, window, headless.Deck1, headless.Deck2, headless.Core, uiClock);
    }

    /// <summary>Capturing a frame takes a few hundred milliseconds, longer than the shrink lasts, so a scenario can
    /// stretch every phase with <c>SHOLTO_HARNESS_SLOWMO</c> (a factor, default 1) to catch the frames in between.</summary>
    private CollapseToIconTimings Slowed(CollapseToIconTimings timings)
    {
        if (!double.TryParse(Environment.GetEnvironmentVariable("SHOLTO_HARNESS_SLOWMO"), out var factor) || factor <= 1)
            return timings;
        return timings with
        {
            Collapse = timings.Collapse * factor,
            Grow = timings.Grow * factor,
            PulsePeriod = timings.PulsePeriod * factor,
        };
    }
}
