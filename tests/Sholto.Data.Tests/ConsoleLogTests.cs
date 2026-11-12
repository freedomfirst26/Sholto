using Sholto.Data;

namespace Sholto.Data.Tests;

/// <summary>The one console adapter: <c>[tag] message</c>, one line per write, at every level, to the writer it
/// was given; the empty tag prints the message alone, so untagged lines stay byte-for-byte.</summary>
public sealed class ConsoleLogTests
{
    private readonly StringWriter _writer = new() { NewLine = "\n" };

    [Theory]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Info)]
    [InlineData(LogLevel.Warn)]
    [InlineData(LogLevel.Error)]
    public void WritesTheTaggedLineAtEveryLevel(LogLevel level)
    {
        new ConsoleLogFactory(_writer).Create("Tools").Write(level, "system check: ok");

        Assert.Equal("[Tools] system check: ok\n", _writer.ToString());
    }

    [Fact]
    public void TheEmptyTagWritesTheMessageAlone()
    {
        new ConsoleLogFactory(_writer).Create("").Warn("No supported controller found — use UI controls.");

        Assert.Equal("No supported controller found — use UI controls.\n", _writer.ToString());
    }

    [Fact]
    public void EachWriteIsOneLine()
    {
        var factory = new ConsoleLogFactory(_writer);

        factory.Create("Bus").Error("first");
        factory.Create("StemMix").Info("second");

        Assert.Equal("[Bus] first\n[StemMix] second\n", _writer.ToString());
    }

    [Fact]
    public void TheExtensionsWriteTheirLevel()
    {
        var log = new LevelLog();

        log.Info("i");
        log.Warn("w");
        log.Error("e");

        Assert.Equal([LogLevel.Info, LogLevel.Warn, LogLevel.Error], log.Levels);
    }

    private sealed class LevelLog : ILog
    {
        public List<LogLevel> Levels { get; } = [];

        public void Write(LogLevel level, string message) => Levels.Add(level);
    }
}
