using Sholto.Data;
using Sholto.App;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>What <see cref="TestMainViewModelFactory"/> builds: the main view model and the headless core
/// it projects (the decks, mixer, library, loader the tests drive directly), and the bus they share.</summary>
internal sealed class TestApp(MainViewModel vm, CoreStack core, DataBus bus)
{
    public MainViewModel Vm { get; } = vm;
    public CoreStack Core { get; } = core;
    public DataBus Bus { get; } = bus;
}
