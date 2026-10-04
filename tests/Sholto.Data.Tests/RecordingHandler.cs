using Sholto.Data;

namespace Sholto.Data.Tests;

public sealed class RecordingHandler<T>(Action<T>? onHandle = null) : IEventHandler<T> where T : struct, IEvent
{
    private readonly Action<T>? _onHandle = onHandle;

    public List<T> Received { get; } = [];

    public void Handle(in T e)
    {
        Received.Add(e);
        _onHandle?.Invoke(e);
    }
}
