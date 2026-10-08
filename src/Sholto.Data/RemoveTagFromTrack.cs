namespace Sholto.Data;

/// <summary>Remove a tag from a track.</summary>
public readonly record struct RemoveTagFromTrack(Guid TrackId, string Name, Origin Origin) : ICommand;
