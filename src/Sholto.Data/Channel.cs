namespace Sholto.Data;

/// <summary>Subscribers (copy-on-write array) for one event type. Facts keep no state.</summary>
internal class Channel<T> where T : struct, IEvent
{
    public IEventHandler<T>[] Handlers = [];

    public virtual void Record(in T e) { }

    public virtual T[] Snapshot() => [];
}
