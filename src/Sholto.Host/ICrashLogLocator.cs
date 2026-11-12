namespace Sholto.Host;

/// <summary>Where crash logs are kept: the <c>logs</c> folder under the app data directory in the app, a temp
/// directory in tests.</summary>
public interface ICrashLogLocator
{
    /// <summary>The folder crash logs are written to and read from. It may not exist yet.</summary>
    string LogsFolder { get; }
}
