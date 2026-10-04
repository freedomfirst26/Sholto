using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>A command handler that keeps every command that reached it, for asserting what an interface sent.</summary>
internal sealed class F9FixRecordingCommandHandler<T> : ICommandHandler<T> where T : struct, ICommand
{
    public List<T> Received { get; } = [];

    public void Handle(in T command) => Received.Add(command);
}
