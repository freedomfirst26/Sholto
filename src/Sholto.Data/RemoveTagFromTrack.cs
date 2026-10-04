namespace Sholto.Data;

/// <summary>Remove a tag from a track.</summary>
public readonly record struct RemoveTagFromTrack(Guid TrackId, string Name, Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
