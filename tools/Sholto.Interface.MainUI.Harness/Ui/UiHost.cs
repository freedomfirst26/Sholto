using Avalonia.Threading;
using Sholto.App;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.App.Lifecycle;
using Sholto.Data;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Bench.Rendering;
using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>
/// Runs a scenario against the real headless UI tree: deck-level actions
/// (load/gain/play/crossfader/wait) land on the captured
/// Deck1 / Deck2 — the exact same <see cref="Deck"/> objects behind the core the mounted
/// <see cref="MainWindow"/> projects — while key/click/screenshot/gesture/midi
/// actions drive the window and the real controller-input stack via
/// <see cref="UiScenarioDriver"/> (which in turn uses <see cref="GestureHost"/>
/// for gesture/midi). The input stack is MainUI's own <see cref="IInputStackFactory"/> with the window
/// as the keyboard, so a "key" step and a "gesture"/"midi" step drive the same performance stack.
/// </summary>
/// <param name="appFactory">Builds the view model + window pair.</param>
/// <param name="deckAdvance">Moves deck position on a "wait" step.</param>
/// <param name="gestureHostFactory">Builds the scripted-surface pipeline each mount drives.</param>
/// <param name="gestureBuilder">Handed to each <see cref="UiScenarioDriver"/>.</param>
/// <param name="snapshot">Handed to each driver.</param>
/// <param name="diff">Handed to each driver.</param>
/// <param name="crossfade">The crossfade curve the scenario runner applies.</param>
/// <param name="inputStackFactory">MainUI's input stack factory, built over the window as the keyboard.</param>
/// <param name="options">The app's configured options the input stack is built with.</param>
/// <param name="controllerThread">The immediate app thread controller input is posted on.</param>
/// <param name="masterCueOutput">Where MASTER CUE takes effect (nowhere, here).</param>
/// <param name="lifecycle">The do-nothing lifecycle: no database, audio device or pickers.</param>
public sealed class UiHost(
    IBenchAppFactory appFactory,
    IDeckAdvance deckAdvance,
    IGestureHostFactory gestureHostFactory,
    IScenarioGestureBuilder gestureBuilder,
    ICoreSnapshot snapshot,
    IStateDiff diff,
    ICrossfadeCurve crossfade,
    IInputStackFactory inputStackFactory,
    SholtoOptions options,
    IAppThread controllerThread,
    IMasterCueOutput masterCueOutput,
    IAppLifecycle lifecycle) : IUiHost
{
    private readonly IBenchAppFactory _appFactory = appFactory;
    private readonly IDeckAdvance _deckAdvance = deckAdvance;
    private readonly IGestureHostFactory _gestureHostFactory = gestureHostFactory;
    private readonly IScenarioGestureBuilder _gestureBuilder = gestureBuilder;
    private readonly ICoreSnapshot _snapshot = snapshot;
    private readonly IStateDiff _diff = diff;
    private readonly ICrossfadeCurve _crossfade = crossfade;
    private readonly IInputStackFactory _inputStackFactory = inputStackFactory;
    private readonly SholtoOptions _options = options;
    private readonly IAppThread _controllerThread = controllerThread;
    private readonly IMasterCueOutput _masterCueOutput = masterCueOutput;
    private readonly IAppLifecycle _lifecycle = lifecycle;

    public (MainViewModel Vm, MainWindow Window, UiScenarioDriver Driver) RunScenario(Scenario scenario)
    {
        var (app, driver) = Mount();

        var decks = new Dictionary<int, Deck> { [1] = app.Deck1, [2] = app.Deck2 };
        var runner = new ScenarioRunner(decks, advance: span => _deckAdvance.Advance(decks.Values, span), _crossfade)
        {
            OnUiAction = driver.Handle,
        };
        runner.Run(scenario);

        return (app.Vm, app.Window, driver);
    }

    public (MainViewModel Vm, MainWindow Window, UiScenarioDriver Driver) Open()
    {
        var (app, driver) = Mount();
        return (app.Vm, app.Window, driver);
    }

    private (BenchApp App, UiScenarioDriver Driver) Mount()
    {
        var app = _appFactory.Create();
        app.Window.Show();
        Dispatcher.UIThread.RunJobs();

        // The window is also the IKeyboard: a "key" scenario step and a "gesture"/"midi" step drive
        // the same performance stack.
        var gestures = _gestureHostFactory.Create();
        var stack = _inputStackFactory.Build(gestures.Surface, app.Core, app.Window,
            _options, gestures.Clock, _controllerThread, _masterCueOutput, _lifecycle);
        gestures.Start(stack.Performance);

        var driver = new UiScenarioDriver(app.Window, app.Vm, gestures, app.Core, [app.Deck1, app.Deck2],
            _gestureBuilder, _snapshot, _diff);
        return (app, driver);
    }
}
