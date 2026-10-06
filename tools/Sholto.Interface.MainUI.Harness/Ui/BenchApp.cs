using Sholto.App;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;
using Sholto.App.Audio;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>
/// What <see cref="BenchAppFactory.Create"/> builds: the view model and window,
/// plus the two concrete <see cref="Deck"/>s behind <c>Deck1</c>/<c>Deck2</c>
/// (the view model only exposes ports; Bench needs the internals) and the headless
/// core the view model projects, and the clock the view models' per-frame work (Glance's re-rank and
/// countdown) runs on.
/// </summary>
public sealed record BenchApp(MainViewModel Vm, MainWindow Window, Deck Deck1, Deck Deck2, CoreStack Core,
    ManualFrameClock UiClock)
{
    /// <summary>Two-part deconstruction, so callers that only want the pair keep compiling.</summary>
    public void Deconstruct(out MainViewModel vm, out MainWindow window)
    {
        vm = Vm;
        window = Window;
    }
}
