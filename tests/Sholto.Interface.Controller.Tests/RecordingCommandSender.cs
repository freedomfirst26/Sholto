using Sholto.Data;

namespace Sholto.Interface.Controller.Tests;

/// <summary>An <see cref="ICommandSender"/> that keeps every command it was sent, boxed (fine in a test).</summary>
internal sealed class RecordingCommandSender : ICommandSender
{
    public List<object> Sent { get; } = [];

    public void Send<T>(in T command) where T : struct, ICommand => Sent.Add(command);
}
