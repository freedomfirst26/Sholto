namespace Sholto.Data;

/// <summary>A frame clock driven by hand: <see cref="Tick"/> runs every subscriber once, in order.
/// For Bench and tests. Built with no start it reads wall time; built with a start it reads that
/// instant until <see cref="Advance"/> moves it, so a caller can tick it by rendered time.</summary>
public sealed class ManualFrameClock : IFrameClock
{
    private readonly FrameTickDispatcher _handlers = new();
    private DateTime? _now;

    /// <summary>A clock whose <see cref="Now"/> is wall time.</summary>
    public ManualFrameClock() { }

    /// <summary>A clock whose <see cref="Now"/> is <paramref name="start"/> until <see cref="Advance"/> moves it.</summary>
    public ManualFrameClock(DateTime start) => _now = start;

    public DateTime Now => _now ?? DateTime.UtcNow;

    public void Subscribe(IFrameTickHandler handler, int order) => _handlers.Add(handler, order);

    public void Tick() => _handlers.Dispatch(Now);

    /// <summary>Moves <see cref="Now"/> on by exactly <paramref name="span"/>. Only a clock built with a
    /// start can be advanced; wall time moves by itself.</summary>
    public void Advance(TimeSpan span)
    {
        if (_now is not { } now)
            throw new InvalidOperationException("A wall-time ManualFrameClock cannot be advanced; build it with a start instant.");
        _now = now + span;
    }
}
