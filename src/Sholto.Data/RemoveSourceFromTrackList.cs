namespace Sholto.Data;

/// <summary>Remove the songs only the source <paramref name="SourceKey"/> (see <see cref="TrackListSource.Key"/>) brought to the Track List.</summary>
/// <param name="Origin">Who sent it.</param>
public readonly record struct RemoveSourceFromTrackList(string SourceKey, Origin Origin) : ICommand;
