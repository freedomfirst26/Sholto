namespace Sholto.Data;

/// <summary>Move a song, by file path, to <paramref name="ToIndex"/> in the Track List's manual order.</summary>
/// <param name="Origin">Who sent it.</param>
public readonly record struct MoveInTrackList(string Path, int ToIndex, Origin Origin) : ICommand;
