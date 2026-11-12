using System.Collections.Concurrent;
using Sholto.Data;

namespace Sholto.TestSupport.Logging;

/// <summary>A log that keeps every line instead of printing it, so a test can pin a line's tag, level and
/// message. Thread-safe: lines go into a <see cref="ConcurrentQueue{T}"/>, shared with every other log its
/// <see cref="RecordingLogFactory"/> made.</summary>
public sealed class RecordingLog(string tag, ConcurrentQueue<LoggedLine> lines) : ILog
{
    private readonly string _tag = tag;
    private readonly ConcurrentQueue<LoggedLine> _lines = lines;

    /// <summary>A log with a queue of its own.</summary>
    public RecordingLog(string tag) : this(tag, new ConcurrentQueue<LoggedLine>())
    {
    }

    /// <summary>Every line written so far, in order.</summary>
    public IReadOnlyList<LoggedLine> Lines => _lines.ToArray();

    public void Write(LogLevel level, string message) => _lines.Enqueue(new LoggedLine(_tag, level, message));
}
