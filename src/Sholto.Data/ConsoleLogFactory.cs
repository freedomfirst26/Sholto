namespace Sholto.Data;

/// <summary>Makes a <see cref="ConsoleLog"/> per tag, every one writing to the same writer — the app's is
/// <see cref="Console.Out"/>, built once at the composition root.</summary>
public sealed class ConsoleLogFactory(TextWriter writer) : IConsoleLogFactory
{
    private readonly TextWriter _writer = writer;

    public ILog Create(string tag) => new ConsoleLog(_writer, tag);
}
