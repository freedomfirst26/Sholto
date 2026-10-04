namespace Sholto.Data;

/// <summary>A deck's per-frame picture: where the playhead is and the flags the waveform and disc draw from.
/// Published on every frame tick while the deck is loaded (and whenever a flag flips). Allocation-free: a
/// struct, no references. State: a late subscriber is told the latest frame.</summary>
/// <param name="PlayPosition">Playhead as a fraction of the track, 0 to 1.</param>
/// <param name="PlaybackSeconds">Playhead in seconds.</param>
/// <param name="PlaybackSpeed">Live playback speed (1.0 = unity): tempo fader and BPM multiplier together.</param>
/// <param name="IsScrubbing">The jog wheel is being turned (the jog seek's flag).</param>
/// <param name="IsScratching">A platter scratch is in flight.</param>
/// <param name="MagneticGlowSec">Time of the beat that should glow green (magnetism active), or -1 for off.</param>
public readonly record struct DeckFrame(
    int Deck, double PlayPosition, double PlaybackSeconds, double PlaybackSpeed,
    bool IsScrubbing, bool IsScratching, double MagneticGlowSec) : IStateEvent
{
    public int Slot => Deck;
}
