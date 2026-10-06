using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>A command handler that appends each command to a log shared with the other handlers, so a test
/// can assert the order commands of different types were sent in.</summary>
internal sealed class LoggingCommandHandler<T>(List<object> log) : ICommandHandler<T> where T : struct, ICommand
{
    private readonly List<object> _log = log;

    public void Handle(in T command) => _log.Add(command);
}
