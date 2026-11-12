namespace Sholto.Data;

/// <summary>The one console adapter: writes each line as <c>[tag] message</c> (just <c>message</c> when the tag
/// is empty) to the writer it was given, at every level, with one <see cref="TextWriter.WriteLine(string)"/> per
/// line so concurrent writers never interleave inside a line (<see cref="Console.Out"/> is synchronized).</summary>
public sealed class ConsoleLog(TextWriter writer, string tag) : ILog
{
    private readonly TextWriter _writer = writer;
    private readonly string _prefix = tag.Length == 0 ? "" : $"[{tag}] ";

    public void Write(LogLevel level, string message) => _writer.WriteLine(_prefix + message);
}
