namespace Sholto.Data;

/// <summary>Removes one handler from a channel (copy-on-write). Disposing twice is harmless.</summary>
internal sealed class Subscription<T>(Channel<T> channel, IEventHandler<T> handler) : IDisposable
    where T : struct, IEvent
{
    private readonly Channel<T> _channel = channel;
    private readonly IEventHandler<T> _handler = handler;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var index = Array.IndexOf(_channel.Handlers, _handler);
        if (index < 0) return;
        var next = new IEventHandler<T>[_channel.Handlers.Length - 1];
        Array.Copy(_channel.Handlers, 0, next, 0, index);
        Array.Copy(_channel.Handlers, index + 1, next, index, next.Length - index);
        _channel.Handlers = next;
    }
}
