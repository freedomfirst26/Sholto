using Sholto.Interface.Controller.WinMm;

namespace Sholto.Interface.Controller.Tests.WinMm;

/// <summary>An <see cref="IWinMmOut"/> that records every packed message, and can be told to fail.</summary>
public sealed class FakeWinMmOut : IWinMmOut
{
    public List<int> Sent { get; } = [];
    public int DisposeCount { get; private set; }
    public Exception? SendFails { get; set; }

    public void Send(int message)
    {
        if (SendFails is not null) throw SendFails;
        Sent.Add(message);
    }

    public void Dispose() => DisposeCount++;
}
