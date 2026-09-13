namespace Sholto.Audio;

/// <summary>
/// Play/pause/seek on a deck's current <c>SoundPlayer</c>. Extracted out of
/// <see cref="Deck"/> — see <see cref="TransportControl"/> for the
/// implementation and why it takes no back-reference to Deck.
/// </summary>
public interface ITransportControl
{
    /// <summary>Start (or resume) playback.</summary>
    void Play();

    /// <summary>Pause playback in place.</summary>
    void Pause();

    /// <summary>Play if paused, pause if playing. No-op (logs) if no track is loaded.</summary>
    void TogglePlay();

    /// <summary>Seek relative to current position by +/- seconds, clamped to track bounds.
    /// Works whether the deck is playing, paused, or finished.</summary>
    void SeekRelative(double seconds);

    /// <summary>Seek to an absolute fraction of the track (0..1). Used by the minimap
    /// click-to-jump.</summary>
    void SeekToFraction(double fraction);
}
