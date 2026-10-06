namespace Sholto.App.Decks;

/// <summary>A deck's three stem groups: the per-stem mute flags and continuous levels, and the state
/// published for the controller's stem pads. Raises <see cref="Changed"/> for each change. Runs on the
/// app thread.</summary>
public interface IDeckStemMix
{
    event Action<DeckChange>? Changed;

    bool DrumsActive { get; set; }
    bool VocalsActive { get; set; }
    bool InstrumentalActive { get; set; }

    double DrumsLevel { get; set; }
    double VocalsLevel { get; set; }
    double InstrumentalLevel { get; set; }

    /// <summary>Return every stem to active and every level to unity, for a freshly loaded track.</summary>
    void ResetForNewTrack();

    /// <summary>Publish each stem's mute state so a subscriber that joins later is replayed it.</summary>
    void PublishCurrent();
}
