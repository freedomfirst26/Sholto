using System.Collections.Concurrent;
using Sholto.Data;

namespace Sholto.TestSupport.Logging;

/// <summary>Makes <see cref="RecordingLog"/>s that all record into one queue, so a test sees every line the code
/// under test wrote, whatever tag it used, in order — and nothing reaches the real console.</summary>
public sealed class RecordingLogFactory : ILogFactory
{
    private readonly ConcurrentQueue<LoggedLine> _lines = new();

    /// <summary>Every line any of this factory's logs wrote, in order.</summary>
    public IReadOnlyList<LoggedLine> Lines => _lines.ToArray();

    public ILog Create(string tag) => new RecordingLog(tag, _lines);
}
