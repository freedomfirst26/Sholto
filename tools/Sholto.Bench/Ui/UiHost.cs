using Avalonia.Threading;
using Sholto.App.ViewModels;
using Sholto.App.Views;
using Sholto.Audio;
using Sholto.Bench.Controller;
using Sholto.Bench.Rendering;
using Sholto.Bench.Scenario;

namespace Sholto.Bench.Ui;

/// <summary>
/// Runs a scenario against the real headless UI tree: deck-level actions
/// (load/gain/play/crossfader/wait) land on <c>vm.Deck1.Player</c> /
/// <c>Deck2.Player</c> — the exact same <see cref="Deck"/> objects the mounted
/// <see cref="MainWindow"/> is bound to — while key/click/screenshot/gesture/midi
/// actions drive the window and the real controller-input stack via
/// <see cref="UiScenarioDriver"/> (which in turn uses <see cref="GestureHost"/>
/// for gesture/midi).
/// </summary>
public static class UiHost
{
    public static (MainViewModel Vm, MainWindow Window, UiScenarioDriver Driver) RunScenario(Scenario.Scenario scenario)
    {
        var (vm, window) = BenchAppComposer.Create();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var decks = new Dictionary<int, Deck> { [1] = vm.Deck1.Player, [2] = vm.Deck2.Player };
        // window is also the IKeyboard: a "key" scenario step and a "gesture"/"midi"
        // step now drive the same Orchestrator instance — see GestureHost's ctor doc.
        var gestureHost = new GestureHost(vm, window);
        var driver = new UiScenarioDriver(window, vm, gestureHost);
        var runner = new ScenarioRunner(decks, advance: span => DeckAdvance.Advance(decks.Values, span))
        {
            OnUiAction = driver.Handle,
        };
        runner.Run(scenario);

        return (vm, window, driver);
    }
}
