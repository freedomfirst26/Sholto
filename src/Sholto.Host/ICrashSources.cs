namespace Sholto.Host;

/// <summary>The three places an unhandled exception surfaces, as plain events so a hook can be tested against a
/// fake. Raising an event must not change what the runtime does next (exit, or not).</summary>
public interface ICrashSources
{
    /// <summary><see cref="AppDomain.UnhandledException"/>: any thread; the process exits afterwards.</summary>
    event Action<Exception>? AppDomainUnhandled;

    /// <summary><see cref="TaskScheduler.UnobservedTaskException"/>: a faulted task nobody awaited; the process
    /// keeps running.</summary>
    event Action<Exception>? TaskUnobserved;

    /// <summary>Avalonia's <c>Dispatcher.UIThread.UnhandledException</c>: left unhandled, so it propagates and
    /// ends the process the way it always did.</summary>
    event Action<Exception>? UiThreadUnhandled;
}
