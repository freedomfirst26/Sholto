namespace Sholto.App.Audio;

/// <summary>What Deck needs of its scratch component beyond the public port:
/// tempo arbitration and the new-track reset.</summary>
// Named like Deck's other component ports (ITransportControl, IStemControl);
// not to be confused with Sholto.App.Performance.ScratchState (platter physics).
public interface IDeckScratchControl : IDeckScratch
{
    /// <summary>True from the first ScratchRate until EndScratch or ResetForNewTrack.</summary>
    bool IsScratching { get; }

    /// <summary>Forget an in-flight scratch when TrackLoading tears the players down.</summary>
    void ResetForNewTrack();
}
