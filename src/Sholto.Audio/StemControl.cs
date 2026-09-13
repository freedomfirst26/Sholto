namespace Sholto.Audio;

/// <summary>
/// The 3 UI stem groups (drums / vocals / instrumental), extracted out of
/// <see cref="Deck"/>. Per-group state for the multiplicative model: audio
/// gain is always <c>active ? curvedLevel : 0</c> so the pad-mute and the
/// level knob act as independent attenuators — same as a Pioneer mixer's
/// channel-mute + channel-fader. Pad-unmute restores whatever level was last
/// set on the knob (no auto-restore-to-unity); if both are zero, audio stays
/// silent and the user has to turn the knob to hear anything (the knob is the
/// source of truth for "where this stem should live").
///
/// The live <see cref="StemMixDataProvider"/> is rebuilt on every
/// Load/LoadStreaming/SwitchToStemMode and owned by <see cref="TrackLoading"/>
/// (see its class doc) — this component takes it as a narrow accessor
/// delegate rather than a back-reference to Deck, same as every sibling
/// component. <see cref="ApplyGroupGain"/> only ever calls
/// <see cref="StemMixDataProvider.SetGain"/>, which does its own
/// <c>Volatile.Write</c> publication for the audio callback — this class adds
/// no locking, allocation, or virtual dispatch of its own on that path.
/// </summary>
internal sealed class StemControl : IStemControl
{
    private readonly Func<StemMixDataProvider?> _stemProvider;

    public StemControl(Func<StemMixDataProvider?> stemProvider)
    {
        _stemProvider = stemProvider;
    }

    private readonly bool[]  _groupActive = { true, true, true };
    private readonly float[] _groupLevel  = { 1f, 1f, 1f };

    /// <inheritdoc/>
    public void SetStemGroup(int group, bool active)
    {
        if (_stemProvider() is null || (uint)group >= 3) return;
        _groupActive[group] = active;
        ApplyGroupGain(group);
    }

    /// <inheritdoc/>
    public void SetStemGroupLevel(int group, double level)
    {
        if (_stemProvider() is null || (uint)group >= 3) return;
        float curved;
        if (level <= 0.04)      curved = 0f;
        else if (level >= 0.5)  curved = 1f;
        else                    curved = (float)((level - 0.04) / (0.5 - 0.04));
        _groupLevel[group] = curved;
        ApplyGroupGain(group);
    }

    private void ApplyGroupGain(int group)
    {
        var stemProvider = _stemProvider();
        if (stemProvider is null) return;
        float gain = _groupActive[group] ? _groupLevel[group] : 0f;
        switch (group)
        {
            case 0: stemProvider.SetGain(StemMixDataProvider.Drums,  gain); break;
            case 1: stemProvider.SetGain(StemMixDataProvider.Vocals, gain); break;
            default:
                stemProvider.SetGain(StemMixDataProvider.Bass,  gain);
                stemProvider.SetGain(StemMixDataProvider.Other, gain);
                break;
        }
    }

    /// <summary>Reset all 3 groups to unmuted/unity, as part of
    /// <see cref="Deck.ResetControls"/>. Not on <see cref="IStemControl"/> —
    /// only Deck calls this, and it bypasses the null-provider guard the
    /// public setters use (matching <c>ResetControls</c>'s original direct
    /// field writes, which applied unconditionally; <see cref="ApplyGroupGain"/>
    /// itself still no-ops when there's no live stem provider).</summary>
    internal void Reset()
    {
        for (int g = 0; g < 3; g++)
        {
            _groupActive[g] = true;
            _groupLevel[g]  = 1f;
            ApplyGroupGain(g);
        }
    }
}
