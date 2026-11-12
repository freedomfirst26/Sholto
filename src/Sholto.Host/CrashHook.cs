namespace Sholto.Host;

/// <summary>Routes every unhandled exception from <see cref="ICrashSources"/> to the crash log writer and says on
/// <c>stderr</c> where it went. It never throws out of a handler and never marks an exception handled, so the
/// process exits (or not) exactly as it would without the hook. A UI-thread exception left unhandled reaches the
/// AppDomain handler too; the second sighting of the same exception object is not logged again.</summary>
public sealed class CrashHook(ICrashSources sources, ICrashLogWriter writer, TextWriter console)
{
    private readonly ICrashSources _sources = sources;
    private readonly ICrashLogWriter _writer = writer;
    private readonly TextWriter _console = console;
    private Exception? _lastLogged;

    public void Install()
    {
        _sources.AppDomainUnhandled += e => Log("AppDomain", e);
        _sources.TaskUnobserved += e => Log("Background task", e);
        _sources.UiThreadUnhandled += e => Log("UI thread", e);
    }

    private void Log(string source, Exception exception)
    {
        try
        {
            if (ReferenceEquals(Interlocked.Exchange(ref _lastLogged, exception), exception))
                return;
            var path = _writer.Write(source, exception);
            _console.WriteLine($"Sholto crashed ({source}); crash log written to {path}");
        }
        catch (Exception failure)
        {
            // A crash handler must not throw: the best left to do is say so, and that may fail too.
            try { _console.WriteLine($"Sholto crashed ({source}) and the crash log could not be written: {failure.Message}"); }
            catch { /* nothing left to report to */ }
        }
    }
}
