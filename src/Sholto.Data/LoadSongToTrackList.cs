namespace Sholto.Data;

/// <summary>Append one song to the Track List, de-duplicated. Songs are keyed by file path. The App handles it.</summary>
/// <param name="Origin">Who sent it.</param>
public readonly record struct LoadSongToTrackList(string Path, Origin Origin) : ICommand;
