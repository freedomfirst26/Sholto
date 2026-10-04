using Sholto.Data;

namespace Sholto.TestSupport;

internal sealed class ThrowingFailureSink : IHandlerFailureSink
{
    public void Report(in HandlerFailure failure) => throw failure.Exception;
}
