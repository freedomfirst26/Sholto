using Sholto.Data;

namespace Sholto.App.Library.Markers;

/// <summary>The hot cues saved per track and pad (0-7).</summary>
public interface IHotCueStore
{
    /// <summary>The track's hot cues, ordered by pad.</summary>
    Task<IReadOnlyList<HotCue>> ListAsync(Guid trackId);

    /// <summary>Save a cue, replacing any hot cue on the same pad of the track.</summary>
    Task SetAsync(Guid trackId, HotCue cue);

    /// <summary>Delete the cue on a pad; nothing happens when absent.</summary>
    Task DeleteAsync(Guid trackId, int pad);
}
