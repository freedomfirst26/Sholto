namespace Sholto.Data;

/// <summary>How much a log line matters. The console adapter prints every level the same way; a level is for
/// tests to pin and for a later adapter to filter on.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}
