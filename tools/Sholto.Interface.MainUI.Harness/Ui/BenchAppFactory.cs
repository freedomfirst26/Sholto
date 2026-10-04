using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Faceplate;
using Sholto.Interface.Faceplate.Devices.DdjFlx4;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.Views;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
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
/// <param name="feature">The feature flags the deck view models are built with.</param>
/// <param name="appThread">The app thread the view models marshal onto.</param>
public sealed class BenchAppFactory(IBenchHeadlessApp headlessApp, IHeadlessCoreFactory coreFactory,
    ICommandSender sender, IQueryAsker asker, IEventSubscriber subscriber, IOptions<FeatureOptions> feature,
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
            subscriber, sender, themeContext, feature, new WaveformPeaksFactory());
        var vm = new MainViewModel(
            themeContext,
            themeCatalog,
            new OverlayViewModelFactory(tagRecency, asker, sender, subscriber, appThread),
            rows,
            sender,
            subscriber,
            appThread,
            new SearchViewModel(rows.Items, new LibrarySearch(), tagRecency, asker, subscriber, appThread),
            new TrackActionsViewModel(),
            new OutputPickerViewModel(),
            deckViewModels.Create(0),
            deckViewModels.Create(1));

        var device = new DdjFlx4Faceplate();
        var overlay = new FaceplateOverlayFactory(new FaceplateDocLoader(), sender, subscriber).Create(device);
        var window = new MainWindow(overlay, themeCatalog) { DataContext = vm };
        return new BenchApp(vm, window, headless.Deck1, headless.Deck2, headless.Core);
    }
}
