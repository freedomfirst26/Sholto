using Sholto.Data;

namespace Sholto.App;

/// <summary>The Inspect gate, installed on the bus path. Every handler registered through it is wrapped in an
/// <see cref="InspectGatedCommandHandler{T}"/>, so while Inspect is on the controller's and keyboard's
/// commands are echoed instead of executed, with no interface knowing about Inspect. The command's name
/// is read off its type once, here at registration, so the echo carries a cached string.</summary>
public sealed class InspectGatedCommandRegistry(
    ICommandRegistry inner,
    IInspectMode inspectMode,
    IEventPublisher publisher,
    string[] gatedInterfaces) : ICommandRegistry
{
    private readonly ICommandRegistry _inner = inner;
    private readonly IInspectMode _inspectMode = inspectMode;
    private readonly IEventPublisher _publisher = publisher;
    private readonly string[] _gatedInterfaces = gatedInterfaces;

    public void Register<T>(ICommandHandler<T> handler) where T : struct, ICommand =>
        _inner.Register<T>(new InspectGatedCommandHandler<T>(
            handler, _inspectMode, _publisher, typeof(T).Name, _gatedInterfaces));
}
