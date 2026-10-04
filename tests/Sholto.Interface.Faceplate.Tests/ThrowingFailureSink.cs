using Sholto.Data;

namespace Sholto.Interface.Faceplate.Tests;

internal sealed class ThrowingFailureSink : IHandlerFailureSink
{
    public void Report(in HandlerFailure failure) => throw failure.Exception;
}
