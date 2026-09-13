namespace Sholto.ExternalTools;

/// <summary>
/// A small bounded ring buffer of the most recent interesting output lines from an
/// external analyser process (demucs, madmom).
///
/// Why this exists: analysers route their subprocess output through a progress parser
/// that drops everything not matching a percentage. A Python traceback — the single
/// most useful thing when demucs breaks — went straight in the bin, and the user was
/// left with "demucs exited with code 1". We now keep the tail of the non-progress
/// output and paste it into the exception and the reporter's failure message.
///
/// Bounded on both axes: at most <see cref="Capacity"/> lines, each truncated to
/// <see cref="MaxLineLength"/> chars, so a chatty or looping subprocess can never
/// grow this without limit.
/// </summary>
public sealed class ProcessOutputTail
{
    public const int MaxLineLength = 400;

    private readonly Queue<string> _lines = new();
    private readonly object _gate = new();

    /// <summary>Maximum number of lines retained.</summary>
    public int Capacity { get; }

    public ProcessOutputTail(int capacity = 20)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
    }

    /// <summary>
    /// Record one output line. Null / blank lines are ignored. Called from the
    /// process's output-reader thread, hence the lock.
    /// </summary>
    public void Add(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var trimmed = line.Trim();
        if (trimmed.Length > MaxLineLength) trimmed = trimmed[..MaxLineLength] + "…";
        lock (_gate)
        {
            _lines.Enqueue(trimmed);
            while (_lines.Count > Capacity) _lines.Dequeue();
        }
    }

    public bool IsEmpty { get { lock (_gate) return _lines.Count == 0; } }

    /// <summary>The retained lines, oldest first, newline-joined.</summary>
    public string Text { get { lock (_gate) return string.Join('\n', _lines); } }

    /// <summary>
    /// <paramref name="message"/> with the retained output appended, ready to hand to
    /// an exception or <c>reporter.Failed</c>. Returns <paramref name="message"/>
    /// unchanged when nothing was captured.
    /// </summary>
    public string Annotate(string message)
    {
        var text = Text;
        return text.Length == 0 ? message : $"{message}\nlast output:\n{text}";
    }
}
