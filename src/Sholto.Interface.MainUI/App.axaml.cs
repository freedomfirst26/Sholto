using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Sholto.Data;
using Sholto.Interface.Controller;
using Sholto.Interface.Controller.Mappings;
using Sholto.App;
using Sholto.App.Lifecycle;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI;

public partial class App : Application
{
    private Sholto.Interface.Controller.IControlSurface? _controller;
    private InputStack? _inputStack;
    private IAppLifecycle? _lifecycle;
    private DispatcherTimer? _statsTimer;
    // Resolves its initial theme through Avalonia's AssetLoader (avares://) — see
    // IThemeContext's doc — so, unlike every other leaf below, it cannot be built
    // in Program.BuildAvaloniaApp's factory lambda (runs before Initialize(), with
    // no live Avalonia application to load bundled themes through). Stays here.
    private IThemeContext? _themeContext;
    private IThemeCatalog? _themeCatalog;
    private ProcessStats? _processStats;
    private Controller.IControllerMappings? _mappingRegistry;
    private IMidiConnection? _midiManager;

    // Which world this process runs against — real FLX4 + real window + real view
    // model, or (later) a scripted stand-in. Chosen once, at the entry point
    // (Program.BuildAvaloniaApp), and handed in here; App itself never picks. The
    // factory only ASSEMBLES the three entities Orchestrator binds — every leaf
    // collaborator below is still built by this class. See ISholtoEntities.
    private readonly ISholtoEntities _entities;

    // The plain-.NET leaves (tool stack, decoder, analysis stack, deck factory,
    // storage, device enumeration, track scanner) — built in Program.BuildAvaloniaApp,
    // before Avalonia's own setup runs, and handed in here. See SholtoStack for what's
    // inside, why it had to move up, and why _themeContext above did NOT move with it.
    private readonly SholtoStack _stack;

    // The configured options objects, built once by the entry point and handed in.
    private readonly SholtoOptions _options;

    // Factories for the collaborators App used to construct itself, built by the
    // composition root (Program.BuildAvaloniaApp, or the designer ctor below).
    private readonly ICoreFactory _coreFactory;
    private readonly IAppLifecycleFactory _lifecycleFactory;
    private readonly ILifecyclePromptsFactory _promptsFactory;
    private readonly IThemeStackFactory _themeStackFactory;
    private readonly IControllerStackFactory _controllerStackFactory;
    private readonly IInputStackFactory _inputStackFactory;
    private readonly IMasterCueOutput _masterCueOutput;
    private readonly IFrameClock _frameClock;
    private readonly IAppThread _appThread;
    private readonly ICommandSender _sender;
    private readonly ITrayFactory _trayFactory;
    private IDisposable? _tray;

    // Second composition root, kept for the Avalonia designer and for
    // AppBuilder.Configure<App>() (Bench's BenchHeadlessApp), which both need a
    // parameterless constructor that still composes something.
    // Avalonia-forced: composes the same graph as Program.BuildAvaloniaApp,
    // via the same AppStackFactory.
    public App() : this(new AppStackFactory().Create()) { }

    public App(AppStack appStack)
    {
        _entities = appStack.Entities;
        _stack = appStack.Stack;
        _options = appStack.Options;
        _coreFactory = appStack.CoreFactory;
        _lifecycleFactory = appStack.LifecycleFactory;
        _promptsFactory = appStack.PromptsFactory;
        _themeStackFactory = appStack.ThemeStackFactory;
        _controllerStackFactory = appStack.ControllerStackFactory;
        _inputStackFactory = appStack.InputStackFactory;
        _masterCueOutput = appStack.MasterCueOutput;
        _frameClock = appStack.FrameClock;
        _appThread = appStack.AppThread;
        _sender = appStack.Sender;
        _trayFactory = appStack.TrayFactory;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Every leaf below except _themeContext was built in
            // Program.BuildAvaloniaApp, before this method (and before
            // Initialize() ever ran) — see SholtoStack for what's inside, in
            // what order, and why.
            var themeStack = _themeStackFactory.Build();
            _themeCatalog = themeStack.Catalog;
            _themeContext = themeStack.Context;

            // Leaves are done. Hand the app-side bundle to the entity factory and let
            // IT assemble the application + window pair — that pairing (the window's
            // DataContext IS the application view model) is the factory's
            // whole reason to exist.
            // The leaves only mean anything to the live assembly; a scripted one
            // brings its own, which is why this is not on ISholtoEntities.
            if (_entities is LiveEntities live)
            {
                live.UseThemeCatalog(_themeCatalog);
                live.UseViewModelStack(new ViewModelStack(
                    _options.Feature,
                    _stack.DeckFactory, _themeContext,
                    _stack.AnalysisStack.SongSegments,
                    _stack.TagRecency,
                    _stack.LibrarySearch,
                    _coreFactory));
            }

            var vm = _entities.CreateApplication();
            var core = _entities.CreateCore();
            desktop.MainWindow = (Avalonia.Controls.Window)_entities.CreateKeyboard();

            // Let the window paint its first frame, THEN initialize services.
            // Posting at Background priority ensures Render runs before InitializeServices.
            // Tray icon: same image as the window icon. Classic, like the window icon
            // (neither follows later theme changes today).
            _tray = _trayFactory.Create(desktop.MainWindow, _themeCatalog.ByName("Classic"));
            desktop.Exit += (_, _) => _tray?.Dispose();

            desktop.MainWindow.Opened += (_, _) =>
                Dispatcher.UIThread.Post(() => InitializeServices(vm, core, desktop),
                    DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ReportConnection(bool connected) =>
        _sender.Send(new ReportDeviceConnection(connected,
            new Origin(InterfaceIds.MainUI, "controller-status", "connection")));

    private void InitializeServices(MainViewModel vm, CoreStack core, IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Surface the boot-time tool probe in the top-bar dot (amber + clickable
        // system report when something is missing). Same value SholtoStackFactory.Build
        // already logged as "[Tools] system check: …" — handed over as a method call
        // rather than a constructor argument so tools/Sholto.Interface.Bench's app factory, which
        // builds MainViewModel with a fixed argument list, keeps working. Nothing
        // here re-probes: ToolSet resolved every path once, at boot.
        vm.ReportSystemCheck(_stack.ToolStack.Check);

        var controllerStack = _controllerStackFactory.Build(_options.Controllers.Value);
        _mappingRegistry = controllerStack.MappingRegistry;

        // The startup sequence (database open after this first paint, theme restore, music folder, audio
        // output) is the headless lifecycle's; this window shows its questions. The prompts listen before
        // the lifecycle starts, so the first question finds them. The controller's sound card is built
        // before audio starts: the output picker and engine both need it to tell the controller's own
        // card apart from ordinary speakers.
        var lifecycle = _lifecycleFactory.Create(core, controllerStack.SoundCard);
        _lifecycle = lifecycle;
        _promptsFactory.Create(vm, desktop.MainWindow!).Start();
        lifecycle.Start();

        _midiManager = controllerStack.Midi;
        // Leaves again: the MIDI stack is App's to build (mappings + manager above),
        // the control surface made out of it is the factory's to assemble.
        if (_entities is LiveEntities live) live.UseMidi(_midiManager);
        _controller = _entities.CreateControlSurface();
        if (!_controller.Connect())
            Console.WriteLine("No supported controller found — use UI controls.");

        // The two entities the input stack binds: the control surface (just created) and the headless
        // core. The keyboard is not one of them: it raises raw key events, which the keyboard interface
        // turns into commands.
        var keyboard = _entities.CreateKeyboard();
        var inputStack = _inputStackFactory.Build(_controller, core, keyboard,
            _options, _frameClock, _appThread, _masterCueOutput, lifecycle);
        _inputStack = inputStack;
        // The magnet's eligibility reaches the view model as an event the App publishes
        // (MagnetEligibilityChanged), so nothing is wired here.

        // Surface controller connection state in the top-bar indicator: report it to the App (which
        // publishes it for any interface to show). Seed with the result of the first attempt above, then
        // follow the supervisor's events, which may fire off the app thread.
        ReportConnection(_controller.IsConnected);
        _controller.ConnectionChanged += connected => _appThread.Post(() => ReportConnection(connected));
        // A controller that comes back comes up dark: resubscribe so the bus replays the
        // current deck state to the adapter. (May fire off the app thread, so hop on.)
        _controller.ConnectionChanged += connected =>
        {
            if (connected) _appThread.Post(inputStack.Feedback.Resubscribe);
        };
        // The LEDs (play, pads, echo, cue, master cue, pad mode) follow state events the App
        // publishes; the controller adapter maps them.
        inputStack.Performance.Tick.Start();

        // SHOLTO_DEBUG_STATS=1 → top-right CPU/RAM readout, sampled once per second.
        _processStats = _stack.ProcessStats;
        if (_processStats.Enabled)
        {
            // Warm-up read so the first displayed value isn't garbage from the
            // long since-startup interval.
            _ = _processStats.Sample();
            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statsTimer.Tick += (_, _) => vm.DebugStats = _processStats.SampleString();
            _statsTimer.Start();
        }

        desktop.Exit += (_, _) =>
        {
            _inputStack?.Dispose();
            _statsTimer?.Stop();
            _lifecycle?.Stop();
            _controller?.Dispose();
        };
    }
}
