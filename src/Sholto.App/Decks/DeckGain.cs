using Sholto.App.Audio;

namespace Sholto.App.Decks;

/// <inheritdoc cref="IDeckGain"/>
public sealed class DeckGain : IDeckGain
{
    private readonly IDeckMixer _mixer;

    // Volume model: deck output = channel fader x crossfade gain.
    private float? _channelGain; // null = UNMEASURED: the FLX-4 fader hasn't been touched yet
    private float _crossfadeGain = 1.0f;

    public DeckGain(IDeckMixer mixer)
    {
        _mixer = mixer;
        // Channel gain starts UNMEASURED (null): the FLX-4 only reports a fader's position when moved, so
        // we don't know it yet. Applying it plays the deck at unity until the fader is first touched; the
        // UI shows no level until measured.
        ApplyVolume();
    }

    public event Action<DeckChange>? Changed;

    /// <summary>Channel fader 0..1, or null until the fader is measured. The FLX-4 only reports a fader's
    /// position on movement, so at startup we don't know it; null is that unmeasured truth (distinct from
    /// 0). Unmeasured plays at unity (so there's sound after a restart); the first move adopts the real
    /// position.</summary>
    public double? ChannelGain
    {
        get => _channelGain;
        set
        {
            var v = value.HasValue ? (float)Math.Clamp(value.Value, 0, 1) : (float?)null;
            if (Nullable.Equals(v, _channelGain)) return;
            _channelGain = v;
            ApplyVolume();
            Changed?.Invoke(DeckChange.ChannelGain);
        }
    }

    /// <summary>False until the channel fader has been measured.</summary>
    public bool GainKnown => _channelGain.HasValue;

    /// <summary>Set when the crossfader moves.</summary>
    public void SetCrossfadeGain(float gain)
    {
        _crossfadeGain = Math.Clamp(gain, 0f, 1f);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        // Unmeasured -> play at UNITY, not silent. The FLX-4 sends no fader position until moved, so gating
        // audio on measurement left every deck dead-silent after a restart until you jiggled the fader.
        // Default audible; the first physical move adopts the real position (soft-takeover has no prior
        // value to take over from). The UI still hides the gain line until GainKnown.
        _mixer.Volume = (_channelGain ?? 1f) * _crossfadeGain;
        Changed?.Invoke(DeckChange.Volume);
    }

    /// <summary>Combined channel x crossfade gain, 0..1 (0 when unmeasured).</summary>
    public double EffectiveGain => (_channelGain ?? 0f) * _crossfadeGain;

    /// <summary>True when the deck is measured AND effectively silent. An unmeasured deck is "unknown",
    /// not "muted".</summary>
    public bool IsMuted => GainKnown && EffectiveGain < 0.001;
}
