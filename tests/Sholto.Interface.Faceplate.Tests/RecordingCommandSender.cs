using Sholto.Data;

namespace Sholto.Interface.Faceplate.Tests;

/// <summary>An <see cref="ICommandSender"/> that remembers what was sent.</summary>
internal sealed class RecordingCommandSender : ICommandSender
{
    public List<object> Sent { get; } = [];

    public void Send<T>(in T command) where T : struct, ICommand => Sent.Add(command);
}
