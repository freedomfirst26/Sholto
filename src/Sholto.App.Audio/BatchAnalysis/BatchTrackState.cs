namespace Sholto.App.Audio.BatchAnalysis;

/// <summary>Where one track of a batch analysis has got to.</summary>
public enum BatchTrackState
{
    /// <summary>The track has taken a slot and its work has begun.</summary>
    Started,
    /// <summary>Beats and key were analysed (or read from the stores) and stored.</summary>
    Finished,
    /// <summary>Beats and key were already stored; nothing was decoded.</summary>
    Skipped,
    /// <summary>Decode or analysis threw, or the batch was cancelled while it ran; see the error.</summary>
    Failed,
}
