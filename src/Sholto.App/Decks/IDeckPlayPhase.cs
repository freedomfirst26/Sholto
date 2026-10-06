using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>A deck's transport phase (stopped, playing, ending) and the end-of-track flash, derived from the
/// play flag and the play position. Raises <see cref="Changed"/> for each change and publishes the phase on
/// the bus. Runs on the app thread; <see cref="PlayPosition"/> and <see cref="UpdateFlash"/> are on the
/// per-frame tick and must not allocate.</summary>
public interface IDeckPlayPhase
{
    event Action<DeckChange>? Changed;

    bool IsPlaying { get; set; }

    PlayPhase PlayState { get; }

    bool EndFlashOn { get; }

    double PlayPosition { get; set; }

    /// <summary>Advance the end-of-track flash from the frame clock. Allocation-free.</summary>
    void UpdateFlash();

    /// <summary>Publish the current phase so a subscriber that joins later is replayed it.</summary>
    void PublishCurrent();
}
