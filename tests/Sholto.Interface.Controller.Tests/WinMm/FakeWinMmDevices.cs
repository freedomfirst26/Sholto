using Sholto.Interface.Controller.WinMm;

namespace Sholto.Interface.Controller.Tests.WinMm;

/// <summary>An <see cref="IWinMmDevices"/> with editable device lists. An open throws the exception set for its
/// index (a real NAudio <c>MmException</c> in the busy tests), else hands out a new fake and records it.</summary>
public sealed class FakeWinMmDevices : IWinMmDevices
{
    public List<string> Inputs { get; } = [];
    public List<string> Outputs { get; } = [];
    public Dictionary<int, Exception> InFails { get; } = [];
    public Dictionary<int, Exception> OutFails { get; } = [];
    public Exception? ListFails { get; set; }
    public Exception? StartFails { get; set; }
    public List<(int Index, FakeWinMmIn Port)> OpenedIns { get; } = [];
    public List<(int Index, FakeWinMmOut Port)> OpenedOuts { get; } = [];

    public IReadOnlyList<string> InputNames() => ListFails is null ? Inputs.ToList() : throw ListFails;

    public IReadOnlyList<string> OutputNames() => ListFails is null ? Outputs.ToList() : throw ListFails;

    public IWinMmIn OpenIn(int index)
    {
        if (InFails.TryGetValue(index, out var fail)) throw fail;
        var port = new FakeWinMmIn { StartFails = StartFails };
        OpenedIns.Add((index, port));
        return port;
    }

    public IWinMmOut OpenOut(int index)
    {
        if (OutFails.TryGetValue(index, out var fail)) throw fail;
        var port = new FakeWinMmOut();
        OpenedOuts.Add((index, port));
        return port;
    }
}
