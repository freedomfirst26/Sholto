using System.Collections.Concurrent;
using Sholto.Data;

namespace Sholto.App.Audio.Tests;

/// <summary>An <see cref="IAppThread"/> whose "app thread" is whichever thread calls <see cref="Drain"/>:
/// posted work waits in a queue until then, and <see cref="IsCurrent"/> is true only while draining.</summary>
internal sealed class QueuedAppThread : IAppThread
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private int _drainingThreadId = -1;

    public bool IsCurrent => Volatile.Read(ref _drainingThreadId) == Environment.CurrentManagedThreadId;

    public int Pending => _queue.Count;

    public void Post(Action action) => _queue.Enqueue(action);

    public void Drain()
    {
        Volatile.Write(ref _drainingThreadId, Environment.CurrentManagedThreadId);
        try
        {
            while (_queue.TryDequeue(out var action)) action();
        }
        finally
        {
            Volatile.Write(ref _drainingThreadId, -1);
        }
    }
}
