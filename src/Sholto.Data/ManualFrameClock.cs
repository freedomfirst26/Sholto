namespace Sholto.Data;

/// <summary>A frame clock driven by hand: <see cref="Tick"/> runs every subscriber once, in order.
/// For Bench and tests.</summary>
public sealed class ManualFrameClock : IFrameClock
{
    private readonly FrameTickDispatcher _handlers = new();

    public DateTime Now => DateTime.UtcNow;

    public void Subscribe(IFrameTickHandler handler, int order) => _handlers.Add(handler, order);

    public void Tick() => _handlers.Dispatch(Now);
}
