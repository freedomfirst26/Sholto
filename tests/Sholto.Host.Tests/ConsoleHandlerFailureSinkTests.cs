using Sholto.Data;

namespace Sholto.Host.Tests;

/// <summary>A bus failure is reported as one error on the <c>Bus</c> log, with the text the console always had.</summary>
public sealed class ConsoleHandlerFailureSinkTests
{
    [Fact]
    public void A_failure_is_an_error_on_the_bus_log()
    {
        var logs = new RecordingLogFactory();

        new ConsoleHandlerFailureSink(logs).Report(new HandlerFailure(typeof(TogglePlay), null, new InvalidOperationException("boom")));

        Assert.Equal([new LoggedLine("Bus", LogLevel.Error, "TogglePlay handler failed: boom")], logs.Lines);
    }
}
