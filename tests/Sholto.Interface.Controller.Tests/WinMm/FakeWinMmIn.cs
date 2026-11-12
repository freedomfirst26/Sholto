using Sholto.Interface.Controller.WinMm;

namespace Sholto.Interface.Controller.Tests.WinMm;

/// <summary>An <see cref="IWinMmIn"/> a test pushes raw short messages through.</summary>
public sealed class FakeWinMmIn : IWinMmIn
{
    public event Action<int>? MessageReceived;

    public bool Started { get; private set; }
    public int DisposeCount { get; private set; }
    public Exception? StartFails { get; set; }

    public void Start()
    {
        if (StartFails is not null) throw StartFails;
        Started = true;
    }

    public void Receive(int raw) => MessageReceived?.Invoke(raw);

    public void Dispose() => DisposeCount++;
}
