using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>An event handler that keeps only the count and the last event, so receiving allocates nothing.</summary>
internal sealed class CountingEventHandler<T> : IEventHandler<T> where T : struct, IEvent
{
    public int Count { get; private set; }
    public T Last { get; private set; }

    public void Handle(in T e)
    {
        Count++;
        Last = e;
    }
}
