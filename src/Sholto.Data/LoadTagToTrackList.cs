namespace Sholto.Data;

/// <summary>Append a snapshot of the songs carrying a tag to the Track List, de-duplicated, as one source.</summary>
/// <param name="Origin">Who sent it.</param>
public readonly record struct LoadTagToTrackList(string Tag, Origin Origin) : ICommand;
