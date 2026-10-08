using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Passes every command on to the real bus and keeps a copy, so a test can count what was sent and
/// still see the App handle it.</summary>
internal sealed class RecordingCommandSender(ICommandSender inner) : ICommandSender
{
    private readonly ICommandSender _inner = inner;

    public List<ICommand> Sent { get; } = [];

    public void Send<T>(in T command) where T : struct, ICommand
    {
        Sent.Add(command);
        _inner.Send(command);
    }
}
