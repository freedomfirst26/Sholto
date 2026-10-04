using Sholto.Data;

namespace Sholto.TestSupport;

internal sealed class RecordingHandler<T> : IEventHandler<T> where T : struct, IEvent
{
    public List<T> Received { get; } = [];

    public void Handle(in T e) => Received.Add(e);
}
