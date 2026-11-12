using Avalonia.Threading;

namespace Sholto.Host;

/// <summary>The real crash sources. Subscribes the AppDomain and task scheduler when built; the UI thread is
/// attached later by <see cref="AttachUiThread"/>, because reading <c>Dispatcher.UIThread</c> before Avalonia's
/// platform setup fixes a null dispatcher for the rest of the process. No handler marks anything handled or
/// observed, so the runtime's exit behaviour is untouched.</summary>
public sealed class CrashSources : ICrashSources
{
    public event Action<Exception>? AppDomainUnhandled;
    public event Action<Exception>? TaskUnobserved;
    public event Action<Exception>? UiThreadUnhandled;

    public CrashSources()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppDomainUnhandled?.Invoke(
            e.ExceptionObject as Exception
            ?? new InvalidOperationException($"Non-exception object thrown: {e.ExceptionObject}"));
        TaskScheduler.UnobservedTaskException += (_, e) => TaskUnobserved?.Invoke(e.Exception);
    }

    /// <summary>Subscribes the UI thread's dispatcher. Call once Avalonia's platform services are set up.</summary>
    public void AttachUiThread(Dispatcher dispatcher) =>
        dispatcher.UnhandledException += (_, e) => UiThreadUnhandled?.Invoke(e.Exception);
}
