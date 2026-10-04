using Sholto.Data;

namespace Sholto.Data.Tests;

public sealed class NoopHandler<T> : IEventHandler<T> where T : struct, IEvent
{
    public void Handle(in T e) { }
}
