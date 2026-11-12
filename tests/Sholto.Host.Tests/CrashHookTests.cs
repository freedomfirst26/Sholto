using Sholto.Host;

namespace Sholto.Host.Tests;

/// <summary>Each crash source reaches the writer once, and the hook never throws back into the runtime.</summary>
public sealed class CrashHookTests
{
    private readonly FakeSources _sources = new();
    private readonly RecordingWriter _writer = new();
    private readonly StringWriter _console = new();

    public CrashHookTests() => new CrashHook(_sources, _writer, _console).Install();

    [Fact]
    public void An_appdomain_exception_is_one_entry()
    {
        var e = new InvalidOperationException("domain");
        _sources.RaiseAppDomain(e);

        Assert.Equal([("AppDomain", (Exception)e)], _writer.Entries);
        Assert.Contains("/logs/crash-test.log", _console.ToString());
    }

    [Fact]
    public void An_unobserved_task_exception_is_one_entry()
    {
        var e = new AggregateException(new InvalidOperationException("task"));
        _sources.RaiseTask(e);

        Assert.Equal([("Background task", (Exception)e)], _writer.Entries);
    }

    [Fact]
    public void A_ui_thread_exception_is_one_entry()
    {
        var e = new InvalidOperationException("ui");
        _sources.RaiseUi(e);

        Assert.Equal([("UI thread", (Exception)e)], _writer.Entries);
    }

    [Fact]
    public void A_ui_thread_exception_that_then_reaches_the_appdomain_is_logged_once()
    {
        var e = new InvalidOperationException("ui then domain");
        _sources.RaiseUi(e);
        _sources.RaiseAppDomain(e);

        Assert.Equal([("UI thread", (Exception)e)], _writer.Entries);
    }

    [Fact]
    public void A_writer_that_throws_does_not_throw_out_of_the_hook()
    {
        _writer.Fail = true;

        var thrown = Record.Exception(() => _sources.RaiseAppDomain(new InvalidOperationException("x")));

        Assert.Null(thrown);
        Assert.Contains("could not be written: disk full", _console.ToString());
    }

    private sealed class FakeSources : ICrashSources
    {
        public event Action<Exception>? AppDomainUnhandled;
        public event Action<Exception>? TaskUnobserved;
        public event Action<Exception>? UiThreadUnhandled;

        public void RaiseAppDomain(Exception e) => AppDomainUnhandled?.Invoke(e);
        public void RaiseTask(Exception e) => TaskUnobserved?.Invoke(e);
        public void RaiseUi(Exception e) => UiThreadUnhandled?.Invoke(e);
    }

    private sealed class RecordingWriter : ICrashLogWriter
    {
        public List<(string, Exception)> Entries { get; } = [];
        public bool Fail { get; set; }

        public string Write(string source, Exception exception)
        {
            if (Fail)
                throw new IOException("disk full");
            Entries.Add((source, exception));
            return "/logs/crash-test.log";
        }
    }
}
