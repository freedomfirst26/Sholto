namespace Sholto.Host;

/// <summary>Names where crash logs live.</summary>
public interface ICrashLogLocatorFactory
{
    /// <summary>The app's own logs folder under the app data directory.</summary>
    ICrashLogLocator AppData();
}
