namespace Sholto.Host;

/// <summary>At start, prints the full path of the newest <c>crash-*.log</c> in the crash log folder, so a crash
/// from an earlier run can be found. Prints nothing when there is none or the folder does not exist.</summary>
public sealed class CrashLogNotice(ICrashLogLocator locator, TextWriter console)
{
    private readonly ICrashLogLocator _locator = locator;
    private readonly TextWriter _console = console;

    public void Announce()
    {
        var newest = Newest();
        if (newest is not null)
            _console.WriteLine($"Sholto crashed before; the latest crash log is {newest}");
    }

    /// <summary>The full path of the newest crash log, or null.</summary>
    public string? Newest()
    {
        var folder = new DirectoryInfo(_locator.LogsFolder);
        if (!folder.Exists)
            return null;
        return folder.EnumerateFiles("crash-*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ThenByDescending(f => f.Name, StringComparer.Ordinal)
            .FirstOrDefault()?.FullName;
    }
}
