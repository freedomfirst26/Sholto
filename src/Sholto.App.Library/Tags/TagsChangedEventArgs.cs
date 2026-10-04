namespace Sholto.App.Library.Tags;

public sealed class TagsChangedEventArgs(Guid trackId, int newCount) : EventArgs
{
    public Guid TrackId { get; } = trackId;
    public int NewCount { get; } = newCount;
}
