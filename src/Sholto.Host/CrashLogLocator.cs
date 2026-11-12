namespace Sholto.Host;

/// <summary>A crash log folder fixed at construction. Built by <see cref="CrashLogLocatorFactory"/>.</summary>
public sealed class CrashLogLocator(string logsFolder) : ICrashLogLocator
{
    private readonly string _logsFolder = logsFolder;

    public string LogsFolder => _logsFolder;
}
