namespace Sholto.Audio;

/// <summary>
/// Implemented by the in-memory data providers that support vinyl-style
/// varispeed reads (<see cref="StemMixDataProvider"/> and
/// <see cref="ScratchDataProvider"/>). Lets <see cref="Deck"/> drive either
/// one through a single reference — from the tempo fader (positive, narrow
/// range) or from a platter scratch (any sign, any magnitude) — without
/// caring which concrete provider is currently backing the SoundPlayer.
/// </summary>
public interface IVarispeedProvider
{
    /// <summary>
    /// Set the playback rate as a signed multiple of unity (1.0 = normal
    /// forward speed, 0 = held/frozen, negative = reverse). Lock-free — the
    /// audio thread reads the value once per buffer, so this is safe to call
    /// from the UI/MIDI thread at any rate.
    /// </summary>
    void SetSpeed(float speed);
}
