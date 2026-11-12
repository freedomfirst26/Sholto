using Sholto.Data;

namespace Sholto.TestSupport.Logging;

/// <summary>One line a <see cref="RecordingLog"/> captured: its tag, level and message.</summary>
public readonly record struct LoggedLine(string Tag, LogLevel Level, string Message);
