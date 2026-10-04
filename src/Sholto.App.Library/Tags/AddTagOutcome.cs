namespace Sholto.App.Library.Tags;

public enum AddTagOutcome
{
    Added,
    AlreadyPresent,
    RejectedEmpty,
    RejectedTooLong,
    RejectedLimitReached,
    RejectedTrackNotFound,
}
