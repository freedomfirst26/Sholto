namespace Sholto.Data;

/// <summary>Shorthands for <see cref="ILog.Write"/> at the levels code writes most.</summary>
public static class LogExtensions
{
    public static void Info(this ILog log, string message) => log.Write(LogLevel.Info, message);

    public static void Warn(this ILog log, string message) => log.Write(LogLevel.Warn, message);

    public static void Error(this ILog log, string message) => log.Write(LogLevel.Error, message);
}
