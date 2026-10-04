using Sholto.Data;

namespace Sholto.Interface.MainUI;

/// <summary>Reports bus failures (a handler that threw, a missing handler) to the console, the way
/// the gesture bus does.</summary>
public sealed class ConsoleHandlerFailureSink : IHandlerFailureSink
{
    public void Report(in HandlerFailure failure) =>
        Console.WriteLine($"[Bus] {failure.MessageType.Name} handler failed: {failure.Exception.Message}");
}
