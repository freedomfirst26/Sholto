namespace Sholto.Data;

/// <summary>What became of an <see cref="AddTagToTrack"/>. <see cref="StoredName"/> is the normalised name when there is one; <see cref="Limit"/> is the tag length limit for RejectedTooLong and the tags-per-track limit for RejectedLimitReached, else 0.</summary>
public readonly record struct TagAddAttempted(Guid TrackId, TagAddOutcome Outcome, string? StoredName, int Limit) : IEvent;
