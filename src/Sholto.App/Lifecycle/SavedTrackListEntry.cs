namespace Sholto.App.Lifecycle;

/// <summary>One saved Track List song: its file path and the keys of the sources that brought it.</summary>
public sealed record SavedTrackListEntry(string Path, IReadOnlyList<string> SourceKeys);
