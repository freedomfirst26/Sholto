using Sholto.Data;

namespace Sholto.App;

/// <summary>Wraps one command's handler with the Inspect gate. Outside Inspect it is one bool check
/// and the call through, no allocation. In Inspect, a command whose origin is one of the gated
/// interfaces is not executed: it is published as <see cref="CommandReceived"/> instead. Everything
/// else (MainUI, Faceplate, Bench) always runs.</summary>
public sealed class InspectGatedCommandHandler<T>(
    ICommandHandler<T> inner,
    IInspectMode inspectMode,
    IEventPublisher publisher,
    string commandName,
    string[] gatedInterfaces) : ICommandHandler<T> where T : struct, ICommand
{
    private readonly ICommandHandler<T> _inner = inner;
    private readonly IInspectMode _inspectMode = inspectMode;
    private readonly IEventPublisher _publisher = publisher;
    private readonly string _commandName = commandName;
    private readonly string[] _gatedInterfaces = gatedInterfaces;

    public void Handle(in T command)
    {
        if (!_inspectMode.IsOn || !IsGated(command.Origin.InterfaceId))
        {
            _inner.Handle(in command);
            return;
        }
        _publisher.Publish(new CommandReceived(command.Origin, _commandName, command.Deck));
    }

    private bool IsGated(string interfaceId)
    {
        for (var i = 0; i < _gatedInterfaces.Length; i++)
            if (string.Equals(_gatedInterfaces[i], interfaceId, StringComparison.Ordinal)) return true;
        return false;
    }
}
