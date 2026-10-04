using System.Collections.Concurrent;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>An app thread that is one real, dedicated thread running posted work in order, like the UI
/// thread in the app. For tests of flows that start on the thread pool and publish on the bus, which
/// assumes a single thread.</summary>
internal sealed class SingleThreadAppThread : IAppThread
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;

    public SingleThreadAppThread()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "test-app-thread" };
        _thread.Start();
    }

    public bool IsCurrent => Thread.CurrentThread == _thread;

    public void Post(Action action) => _queue.Add(action);

    private void Run()
    {
        foreach (var action in _queue.GetConsumingEnumerable()) action();
    }
}
