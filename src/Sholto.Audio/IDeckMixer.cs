namespace Sholto.Audio;

/// <summary>
/// A deck's gain and cue routing: fader volume, master-path gain, and PFL cue.
/// The DSP chain (EQ / filter / echo) lives on <see cref="IDeckEffects"/>
/// instead — this port is exactly the subset <see cref="IMixSource"/> (the
/// side <c>CueOutputRouter</c> actually consumes) already needs. See
/// <see cref="DeckMixer"/> for the implementation moved out of <c>Deck</c>.
/// </summary>
public interface IDeckMixer
{
    /// <summary>Linear gain [0..1]. Applied to the master path (channel ×
    /// crossfade) — read lock-free, once per audio buffer, by
    /// <c>CueOutputRouter</c> via <see cref="Deck.MasterGain"/>.</summary>
    float Volume { get; set; }

    /// <summary>Master-path gain (channel × crossfade), read lock-free by
    /// <c>CueOutputRouter</c>.</summary>
    float MasterGain { get; }

    /// <summary>PFL cue selection — true if this deck is summed into the
    /// headphone (ch3-4) mix at full level, pre-fader.</summary>
    bool CueActive { get; set; }

    /// <summary>Fires when cue membership changes.</summary>
    event Action<bool>? CueChanged;

    /// <summary>Flip <see cref="CueActive"/>.</summary>
    void ToggleCue();
}
