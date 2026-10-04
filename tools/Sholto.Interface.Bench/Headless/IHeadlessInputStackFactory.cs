using Sholto.App;
using Sholto.App.Performance;
using Sholto.Data;
using Sholto.Interface.Bench.Controller;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Builds the controller-driven input stack over the headless core: the same wiring the app's
/// input stack does, minus the keyboard (a UI interface; the MainUI harness drives that one).</summary>
public interface IHeadlessInputStackFactory
{
    /// <summary>Builds the stack over <paramref name="gestures"/>' surface and clock, connects the
    /// surface and starts the performance tick. <paramref name="appThread"/> is the thread controller
    /// input is posted on (the immediate one, in Bench).</summary>
    PerformanceStack Build(CoreStack core, GestureHost gestures, IAppThread appThread);
}
