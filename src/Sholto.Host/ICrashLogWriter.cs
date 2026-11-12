namespace Sholto.Host;

/// <summary>Writes one crash entry for an unhandled exception.</summary>
public interface ICrashLogWriter
{
    /// <summary>Appends an entry for <paramref name="exception"/>, raised by <paramref name="source"/>, and
    /// returns the full path of the file written. May throw on I/O failure; callers in a crash path catch.</summary>
    string Write(string source, Exception exception);
}
