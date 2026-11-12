using System.Globalization;
using System.Text;

namespace Sholto.Host;

/// <summary>Appends crash entries to <c>crash-&lt;yyyy-MM-dd&gt;.log</c> (UTC date) in the crash log folder,
/// creating the folder if it is missing. An entry holds the UTC time, the app version, the source, and
/// <see cref="Exception.ToString"/>: type, message and full stack, inner exceptions included.</summary>
public sealed class CrashLogWriter(ICrashLogLocator locator, ICrashAppVersion version, TimeProvider time) : ICrashLogWriter
{
    private readonly ICrashLogLocator _locator = locator;
    private readonly ICrashAppVersion _version = version;
    private readonly TimeProvider _time = time;

    public string Write(string source, Exception exception)
    {
        var now = _time.GetUtcNow();
        var folder = _locator.LogsFolder;
        Directory.CreateDirectory(folder);
        var path = Path.GetFullPath(Path.Combine(folder,
            $"crash-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log"));

        var entry = new StringBuilder()
            .AppendLine("=== Sholto crash ===")
            .AppendLine($"Time (UTC): {now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}")
            .AppendLine($"Version: {_version.Version}")
            .AppendLine($"Source: {source}")
            .AppendLine($"Exception: {exception.GetType().FullName}: {exception.Message}")
            .AppendLine(exception.ToString())
            .AppendLine()
            .ToString();

        File.AppendAllText(path, entry, Encoding.UTF8);
        return path;
    }
}
