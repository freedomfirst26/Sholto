using Sholto.Data;

namespace Sholto.Data.Tests;

public sealed class PingHandler : ICommandHandler<Ping>
{
    public int Count;
    public Ping Last;
    public Action? OnHandle;

    public void Handle(in Ping command)
    {
        Count++;
        Last = command;
        OnHandle?.Invoke();
    }
}
