using Sholto.Data;
using Sholto.Interface.Keyboard;
using Sholto.Interface.Controller;
using Sholto.Interface.Faceplate;
using Sholto.Interface.Faceplate.Devices.DdjFlx4;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.Views;
using Sholto.App;
using Sholto.App.Decks;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI;

/// <summary>The real assembly of the three entities: a real DDJ-FLX4
/// <see cref="Controller"/>, the real <see cref="MainWindow"/> as the keyboard (raw key events, recognised into gestures by the composition root), and
/// a real <see cref="MainViewModel"/> over the headless core. Constructed by
/// <c>Program.BuildAvaloniaApp</c> and handed to <c>App</c>'s constructor (Avalonia's
/// <c>AppBuilder.Configure&lt;TApp&gt;(Func&lt;TApp&gt;)</c> overload makes that
/// possible), so the choice of "which world does this process run against" is made
/// at the entry point and nowhere else.
///
/// <para><b>Why the Use… seeders exist.</b> This factory is created before any leaf
/// exists — <c>App</c> is the thing that builds the leaves, and <c>App</c> is
/// constructed after this. So the composition root hands each bundle of leaves over
/// when it has them (<see cref="UseViewModelStack"/> during framework init,
/// <see cref="UseMidi"/> once the MIDI stack is up after first paint) and the factory
/// decides how leaves become entities. Deliberately NOT on
/// <see cref="ISholtoEntities"/>: that interface stays three factory methods, and a
/// scripted implementation has an entirely different set of leaves.</para>
///
/// <para>Each Create… is idempotent — repeated calls return the SAME entity. That is
/// what makes the three consistent: <see cref="CreateKeyboard"/> returns the window
/// whose DataContext is exactly the object <see cref="CreateApplication"/> returns,
/// so a keyboard shortcut and a controller gesture cannot end up acting on two
/// different view models.</para></summary>
public sealed class LiveEntities(
    ICommandSender sender,
    IQueryAsker asker,
    IEventSubscriber subscriber,
    IEventPublisher publisher,
    IFrameClock clock,
    IAppThread appThread) : ISholtoEntities
{
    private readonly ICommandSender _sender = sender;
    private readonly IQueryAsker _asker = asker;
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly IEventPublisher _publisher = publisher;
    private readonly IFrameClock _clock = clock;
    private readonly IAppThread _appThread = appThread;
    private ViewModelStack? _leaves;
    private IMidiConnection? _midi;
    private IThemeCatalog? _themeCatalog;
    private MainViewModel? _app;
    private CoreStack? _core;
    private MainWindow? _keyboard;
    private IControlSurface? _surface;

    /// <summary>Composition root → factory: the leaves the real app is built from.
    /// Must be called before <see cref="CreateApplication"/>.</summary>
    public void UseViewModelStack(ViewModelStack leaves) => _leaves = leaves;

    /// <summary>Composition root → factory: the theme catalog the view model and
    /// window start from. Must be called before <see cref="CreateApplication"/> and
    /// <see cref="CreateKeyboard"/>. Separate from <see cref="UseViewModelStack"/>
    /// because the catalog can only be built once Avalonia is up.</summary>
    public void UseThemeCatalog(IThemeCatalog themeCatalog) => _themeCatalog = themeCatalog;

    /// <summary>Composition root → factory: the MIDI stack the real control surface
    /// talks through. Must be called before <see cref="CreateControlSurface"/>.
    /// Separate from <see cref="UseViewModelStack"/> only because the MIDI stack
    /// is deliberately built late, after the window has painted its first frame.</summary>
    public void UseMidi(IMidiConnection midi) => _midi = midi;

    public MainViewModel CreateApplication() => Application;

    public CoreStack CreateCore()
    {
        _ = Application;
        return _core!;
    }

    /// <summary>The main view model, built over the headless core (<see cref="CreateCore"/>).</summary>
    public MainViewModel Application => _app ??= BuildApplication();

    private MainViewModel BuildApplication()
    {
        var l = _leaves ?? throw new InvalidOperationException(
            $"{nameof(UseViewModelStack)} must be called before the application is created.");
        var catalog = _themeCatalog ?? throw new InvalidOperationException(
            $"{nameof(UseThemeCatalog)} must be called before the application is created.");
        // The sessions publish the decks' state onto the bus from construction, so they are built here and
        // handed to the core (which drives them) and their view models (which project them).
        var sessions = new DeckSessionFactory(l.Decks, l.SongSegments, _clock, _appThread, _publisher);
        var deck1 = sessions.Create(0);
        var deck2 = sessions.Create(1);
        var core = l.CoreFactory.Build(deck1, deck2);
        _core = core;
        // The view models know the bus and nothing else of the App: they subscribe to its events and send
        // it commands and queries.
        var rows = new LibraryRowsViewModel(_subscriber, new TrackRowFactory(l.Theme));
        var search = new SearchViewModel(rows.Items, l.LibrarySearch, l.TagRecency, _asker, _subscriber, _appThread);
        var deckViewModels = new DeckViewModelFactory(_subscriber, _sender, l.Theme, l.Features,
            new Sholto.App.Analysis.Analyzers.Waveform.WaveformPeaksFactory());
        return new MainViewModel(
            l.Theme, catalog,
            new OverlayViewModelFactory(l.TagRecency, _asker, _sender, _subscriber, _appThread),
            rows, _sender, _subscriber, _appThread,
            search, new TrackActionsViewModel(),
            deckViewModels.Create(0), deckViewModels.Create(1));
    }

    public IKeyboard CreateKeyboard() => Window;

    /// <summary>The same object as <see cref="CreateKeyboard"/>, typed as the window
    /// Avalonia's desktop lifetime needs to be handed. Built with the application
    /// entity as its DataContext — the pairing this factory exists to guarantee.</summary>
    public MainWindow Window => _keyboard ??= BuildWindow();

    private MainWindow BuildWindow()
    {
        var device = new DdjFlx4Faceplate();
        var overlay = new FaceplateOverlayFactory(new FaceplateDocLoader(), _sender, _subscriber).Create(device);
        var catalog = _themeCatalog ?? throw new InvalidOperationException(
            $"{nameof(UseThemeCatalog)} must be called before the window is created.");
        return new MainWindow(overlay, catalog) { DataContext = Application };
    }

    public IControlSurface CreateControlSurface() =>
        _surface ??= new Sholto.Interface.Controller.Controller(
            _midi ?? throw new InvalidOperationException(
                $"{nameof(UseMidi)} must be called before the control surface is created."));
}
