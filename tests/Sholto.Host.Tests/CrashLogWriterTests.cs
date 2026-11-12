using System.Diagnostics;
using System.Reflection;
using Sholto.Host;

namespace Sholto.Host.Tests;

/// <summary>A crash entry lands in crash-&lt;UTC date&gt;.log in the logs folder, with time, version and the full
/// exception, appended when the file already exists.</summary>
public sealed class CrashLogWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sholto-crash-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Writes_time_version_type_message_and_stack_with_inner_exceptions_into_a_new_folder()
    {
        var logs = Path.Combine(_root, "sholto", "logs");
        var writer = Writer(logs, new DateTimeOffset(2026, 11, 12, 23, 30, 5, TimeSpan.FromHours(-5)));

        var path = writer.Write("UI thread", Thrown());

        Assert.Equal(Path.Combine(logs, "crash-2026-11-13.log"), path);
        var text = File.ReadAllText(path);
        Assert.Contains("Time (UTC): 2026-11-13T04:30:05.0000000Z", text);
        Assert.Contains("Version: 1.2.3-test", text);
        Assert.Contains("Source: UI thread", text);
        Assert.Contains("Exception: System.InvalidOperationException: outer boom", text);
        Assert.Contains("---> System.IO.IOException: inner boom", text);
        Assert.Contains(nameof(ThrowInner), text);
        Assert.Contains(nameof(Thrown), text);
    }

    [Fact]
    public void A_second_crash_the_same_day_is_appended_to_the_same_file()
    {
        var writer = Writer(_root, new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

        var first = writer.Write("AppDomain", new InvalidOperationException("first"));
        var second = writer.Write("Background task", new InvalidOperationException("second"));

        Assert.Equal(first, second);
        Assert.Equal(["crash-2026-11-12.log"], Directory.GetFiles(_root).Select(Path.GetFileName));
        var text = File.ReadAllText(first);
        Assert.Equal(2, text.Split("=== Sholto crash ===").Length - 1);
        Assert.True(text.IndexOf("first", StringComparison.Ordinal) < text.IndexOf("second", StringComparison.Ordinal));
    }

    [Fact]
    public void The_version_is_the_assembly_informational_version_or_unknown()
    {
        var expected = typeof(CrashAppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.Equal(expected, new CrashAppVersion(typeof(CrashAppVersion).Assembly).Version);
        Assert.Equal("unknown", new CrashAppVersion(null).Version);
    }

    private static CrashLogWriter Writer(string folder, DateTimeOffset now) =>
        new(new CrashLogLocator(folder), new FixedVersion(), new FixedTime(now));

    private static Exception Thrown()
    {
        try
        {
            try { ThrowInner(); }
            catch (Exception inner) { throw new InvalidOperationException("outer boom", inner); }
        }
        catch (Exception e) { return e; }
        throw new UnreachableException();
    }

    private static void ThrowInner() => throw new IOException("inner boom");

    private sealed class FixedVersion : ICrashAppVersion
    {
        public string Version => "1.2.3-test";
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();
    }
}
