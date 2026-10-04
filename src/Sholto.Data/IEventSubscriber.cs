namespace Sholto.Data;

/// <summary>Interface-facing: subscribe to events. Current state events are replayed to the new
/// handler before Subscribe returns. Dispose the result to unsubscribe.</summary>
public interface IEventSubscriber
{
    IDisposable Subscribe<T>(IEventHandler<T> handler) where T : struct, IEvent;
}
