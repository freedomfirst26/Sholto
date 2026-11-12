namespace Sholto.Host;

/// <summary>Names where crash logs live.</summary>
public sealed class CrashLogLocatorFactory
{
    /// <summary>The app's own logs folder: <c>&lt;LocalApplicationData&gt;/sholto/logs</c>, beside the library
    /// database and the analysis cache.</summary>
    public ICrashLogLocator AppData() => new CrashLogLocator(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sholto", "logs"));
}
