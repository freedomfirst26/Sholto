using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>Mounts the headless UI tree and drives scenarios against it.</summary>
public interface IUiHost
{
    /// <summary>Mounts the UI and runs <paramref name="scenario"/> against it.</summary>
    (MainViewModel Vm, MainWindow Window, UiScenarioDriver Driver) RunScenario(Scenario scenario);

    /// <summary>Mounts the UI with no scenario run, ready to drive or capture.</summary>
    (MainViewModel Vm, MainWindow Window, UiScenarioDriver Driver) Open();
}
