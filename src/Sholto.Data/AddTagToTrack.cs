namespace Sholto.Data;

/// <summary>Tag a track. The outcome is reported by <see cref="TagAddAttempted"/>.</summary>
public readonly record struct AddTagToTrack(Guid TrackId, string Name, Origin Origin) : ICommand;
