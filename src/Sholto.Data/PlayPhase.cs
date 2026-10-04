namespace Sholto.Data;

/// <summary>Where a deck is in its transport: not playing, playing, or playing in the last stretch
/// of the track (the end-of-track warning).</summary>
public enum PlayPhase
{
    Stopped,
    Playing,
    Ending,
}
