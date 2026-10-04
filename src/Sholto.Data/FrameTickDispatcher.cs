namespace Sholto.Data;

/// <summary>Ordered handler list shared by frame clock implementations. Copy-on-write, so a handler
/// may subscribe during a tick. Stable: equal orders keep subscription order.</summary>
public sealed class FrameTickDispatcher
{
    private (int Order, IFrameTickHandler Handler)[] _handlers = [];

    public void Add(IFrameTickHandler handler, int order)
    {
        var next = new (int Order, IFrameTickHandler Handler)[_handlers.Length + 1];
        var i = 0;
        while (i < _handlers.Length && _handlers[i].Order <= order) { next[i] = _handlers[i]; i++; }
        next[i] = (order, handler);
        for (; i < _handlers.Length; i++) next[i + 1] = _handlers[i];
        _handlers = next;
    }

    public void Dispatch(DateTime now)
    {
        foreach (var (_, handler) in _handlers) handler.OnFrame(now);
    }
}
