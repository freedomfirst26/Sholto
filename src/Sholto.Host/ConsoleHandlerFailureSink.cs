using Sholto.Data;

namespace Sholto.Host;

/// <summary>Reports bus failures (a handler that threw, a missing handler) as errors on the <c>Bus</c> log, the
/// way the gesture bus does.</summary>
public sealed class ConsoleHandlerFailureSink(ILogFactory logs) : IHandlerFailureSink
{
    private readonly ILog _log = logs.Create("Bus");

    public void Report(in HandlerFailure failure) =>
        _log.Error($"{failure.MessageType.Name} handler failed: {failure.Exception.Message}");
}
