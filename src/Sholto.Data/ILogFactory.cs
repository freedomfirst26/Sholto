namespace Sholto.Data;

/// <summary>Makes the <see cref="ILog"/> for one tag (the <c>[Tag]</c> a line starts with). The empty tag makes
/// a log whose lines carry no tag at all.</summary>
public interface ILogFactory
{
    ILog Create(string tag);
}
