namespace Sholto.Data;

/// <summary>Receives event <typeparamref name="T"/>. Runs synchronously on the publisher's thread:
/// an interface that needs another thread marshals itself via <see cref="IAppThread"/>.</summary>
public interface IEventHandler<T> where T : struct, IEvent
{
    void Handle(in T e);
}
