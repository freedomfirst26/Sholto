using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>A command handler that keeps every command it receives, in order.</summary>
internal sealed class F9RecordingCommandHandler<T> : ICommandHandler<T> where T : struct, ICommand
{
    public List<T> Received { get; } = [];

    public void Handle(in T command) => Received.Add(command);
}
