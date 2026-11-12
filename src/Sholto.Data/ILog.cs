namespace Sholto.Data;

/// <summary>One tagged log: where a class writes its diagnostic lines. Made by an <see cref="ILogFactory"/>, which
/// fixes the tag; the class writes only the level and the message.</summary>
public interface ILog
{
    void Write(LogLevel level, string message);
}
