namespace Sholto.Data;

/// <summary>The tags on a track, alphabetical. Empty without a database.</summary>
public readonly record struct GetTrackTags(Guid TrackId) : IQuery<Task<IReadOnlyList<string>>>;
