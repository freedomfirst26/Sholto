namespace Sholto.App.Library;

/// <summary>One song in the Track List: its file path and the keys of every source that brought it in
/// (<c>crate:{id}</c>, <c>tag:{lowercased}</c>, <c>songs</c>). A song stays while it has at least one source.</summary>
public sealed class TrackListEntry(string path, IEnumerable<string> sourceKeys)
{
    public string Path { get; } = path;

    public HashSet<string> SourceKeys { get; } = [.. sourceKeys];
}
