using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>Remembers which command types got a handler.</summary>
internal sealed class RecordingCommandRegistry : ICommandRegistry
{
    public HashSet<Type> Registered { get; } = [];

    public void Register<T>(ICommandHandler<T> handler) where T : struct, ICommand => Registered.Add(typeof(T));
}
