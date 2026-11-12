using Sholto.Host;

namespace Sholto.Host.Tests;

/// <summary>At start the newest crash log's full path is printed; nothing is printed when there is none.</summary>
public sealed class CrashLogNoticeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sholto-notice-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _console = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Prints_the_full_path_of_the_newest_crash_log()
    {
        Directory.CreateDirectory(_root);
        var older = Touch("crash-2026-11-13.log", new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));
        var newer = Touch("crash-2026-11-11.log", new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc));
        Touch("other.log", new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc));

        Notice().Announce();

        Assert.Contains(Path.GetFullPath(newer), _console.ToString());
        Assert.DoesNotContain(older, _console.ToString());
        Assert.DoesNotContain("other.log", _console.ToString());
    }

    [Fact]
    public void Prints_nothing_when_the_folder_has_no_crash_log()
    {
        Directory.CreateDirectory(_root);
        Touch("other.log", DateTime.UtcNow);

        Notice().Announce();

        Assert.Equal("", _console.ToString());
    }

    [Fact]
    public void Prints_nothing_when_the_folder_does_not_exist()
    {
        Notice().Announce();

        Assert.Equal("", _console.ToString());
        Assert.False(Directory.Exists(_root));
    }

    private CrashLogNotice Notice() => new(new CrashLogLocator(_root), _console);

    private string Touch(string name, DateTime writtenUtc)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTimeUtc(path, writtenUtc);
        return path;
    }
}
