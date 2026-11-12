using System.Collections.Concurrent;
using Sholto.Data;
using Sholto.TestSupport.Logging;

namespace Sholto.Data.Tests;

/// <summary>The recording log tests pin lines through: it keeps each line's tag, level and message, a factory's
/// logs share one queue, and parallel writers lose nothing.</summary>
public sealed class RecordingLogTests
{
    [Fact]
    public void KeepsTheTagLevelAndMessage()
    {
        var log = new RecordingLog("Bus");

        log.Error("Ping handler failed: boom");

        Assert.Equal([new LoggedLine("Bus", LogLevel.Error, "Ping handler failed: boom")], log.Lines);
    }

    [Fact]
    public void WritesIntoTheQueueItWasGiven()
    {
        var lines = new ConcurrentQueue<LoggedLine>();

        new RecordingLog("A", lines).Info("one");
        new RecordingLog("B", lines).Warn("two");

        Assert.Equal([new LoggedLine("A", LogLevel.Info, "one"), new LoggedLine("B", LogLevel.Warn, "two")], lines);
    }

    [Fact]
    public void TheFactorysLogsShareOneQueueInOrder()
    {
        var logs = new RecordingLogFactory();

        logs.Create("Tools").Info("system check: ok");
        logs.Create("").Warn("No supported controller found — use UI controls.");
        logs.Create("StemMix").Write(LogLevel.Debug, "d");

        Assert.Equal(
        [
            new LoggedLine("Tools", LogLevel.Info, "system check: ok"),
            new LoggedLine("", LogLevel.Warn, "No supported controller found — use UI controls."),
            new LoggedLine("StemMix", LogLevel.Debug, "d"),
        ], logs.Lines);
    }

    [Fact]
    public void ParallelWritesLoseNothing()
    {
        var logs = new RecordingLogFactory();
        const int writers = 8;
        const int perWriter = 5_000;

        Parallel.For(0, writers, new ParallelOptions { MaxDegreeOfParallelism = writers }, writer =>
        {
            var log = logs.Create($"W{writer}");
            for (var i = 0; i < perWriter; i++)
                log.Info(i.ToString());
        });

        Assert.Equal(writers * perWriter, logs.Lines.Count);
        for (var writer = 0; writer < writers; writer++)
        {
            var tag = $"W{writer}";
            Assert.Equal(
                Enumerable.Range(0, perWriter).Select(i => i.ToString()),
                logs.Lines.Where(line => line.Tag == tag).Select(line => line.Message));
        }
    }
}
