using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>
/// Pins <c>Dispatcher.UIThread</c> before any test runs. Avalonia 11.3 creates it lazily, and the
/// thread that first touches it owns it. With xUnit's parallel workers, whichever of "construct a
/// brush" or "AvaloniaTestApp setup" ran first decided the owner; when setup won, every other worker
/// got "Call from invalid thread", cascading into "No themes loaded".
/// </summary>
internal static class AvaloniaDispatcherPin
{
    // [ModuleInitializer] requires a static method: forced by the language/framework.
    [ModuleInitializer]
    internal static void Pin() => _ = Dispatcher.UIThread;
}
