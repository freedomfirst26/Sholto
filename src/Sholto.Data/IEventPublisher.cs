namespace Sholto.Data;

/// <summary>App-facing: publish an event to every subscriber, synchronously, in subscription order.</summary>
public interface IEventPublisher
{
    void Publish<T>(in T e) where T : struct, IEvent;
}
