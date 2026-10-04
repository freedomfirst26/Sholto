using Sholto.Interface.Controller;
using Sholto.Interface.Controller.Mappings.DdjFlx4;

namespace Sholto.Interface.Controller.Tests;

/// <summary>An <see cref="IMidiConnection"/> over the real DDJ-FLX4 mapping that records every byte
/// array sent and lets a test raise device events.</summary>
public sealed class FakeMidiConnection : IMidiConnection
{
    public List<byte[]> Sent { get; } = [];

    public event Action<ControllerEvent>? EventReceived;
    public event Action? Connected;
    public event Action? ConnectionLost { add { } remove { } }

    public bool IsConnected => true;
    public IControllerMapping? Mapping { get; } = new DdjFlx4Mapping();

    public void Send(byte[] bytes) => Sent.Add(bytes);
    public bool Connect() => true;
    public void Raise(ControllerEvent evt) => EventReceived?.Invoke(evt);
    public void RaiseConnected() => Connected?.Invoke();
    public void Dispose() { }
}
