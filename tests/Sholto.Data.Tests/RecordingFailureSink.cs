using Sholto.Data;

namespace Sholto.Data.Tests;

public sealed class RecordingFailureSink : IHandlerFailureSink
{
    public List<HandlerFailure> Failures { get; } = [];

    public void Report(in HandlerFailure failure) => Failures.Add(failure);
}
