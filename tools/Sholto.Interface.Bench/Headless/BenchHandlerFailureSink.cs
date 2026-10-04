using Sholto.Data;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Reports bus failures (a handler that threw, a missing handler) to the console.</summary>
public sealed class BenchHandlerFailureSink : IHandlerFailureSink
{
    public void Report(in HandlerFailure failure) =>
        Console.WriteLine($"[Bus] {failure.MessageType.Name} handler failed: {failure.Exception.Message}");
}
