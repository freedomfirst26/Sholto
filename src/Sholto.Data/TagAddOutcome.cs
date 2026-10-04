namespace Sholto.Data;

/// <summary>What became of an <see cref="AddTagToTrack"/>, reported by <see cref="TagAddAttempted"/>.</summary>
public enum TagAddOutcome
{
    Added,
    AlreadyPresent,
    RejectedEmpty,
    RejectedTooLong,
    RejectedLimitReached,
    RejectedTrackNotFound,
    /// <summary>There is no database, so there is no tag service to ask.</summary>
    Unavailable,
}
