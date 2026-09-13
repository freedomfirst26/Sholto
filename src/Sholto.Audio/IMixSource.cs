using SoundFlow.Abstracts;

namespace Sholto.Audio;

/// <summary>
/// A source of post-EQ stereo for <see cref="CueOutputRouter"/> to pull and mix.
/// <see cref="Deck"/> is the only production implementation; the narrow surface
/// (exactly the three members <see cref="CueOutputRouter"/> uses) is what lets a
/// test harness feed the router synthetic sources with no real deck behind them.
/// </summary>
public interface IMixSource
{
    /// <summary>The component to pull post-EQ, full-level stereo from via
    /// <see cref="SoundComponent.Process"/> (fader NOT applied — see
    /// <see cref="MasterGain"/>).</summary>
    SoundComponent Component { get; }

    /// <summary>PFL cue selection — true if this source is summed into the
    /// headphone (ch3-4) mix at full level, pre-fader.</summary>
    bool CueActive { get; }

    /// <summary>Master-path gain (channel × crossfade), applied to ch1-2.</summary>
    float MasterGain { get; }
}
