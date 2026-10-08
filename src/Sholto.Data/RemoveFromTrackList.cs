namespace Sholto.Data;

/// <summary>Remove one song, by file path, from the Track List.</summary>
/// <param name="Origin">Who sent it.</param>
public readonly record struct RemoveFromTrackList(string Path, Origin Origin) : ICommand;
