using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>A command handler that only counts how many commands reached it; allocates nothing per call.</summary>
internal sealed class CountingCommandHandler<T> : ICommandHandler<T> where T : struct, ICommand
{
    public int Count { get; private set; }

    public void Handle(in T command) => Count++;
}
