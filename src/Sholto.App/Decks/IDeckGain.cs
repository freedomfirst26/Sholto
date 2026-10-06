namespace Sholto.App.Decks;

/// <summary>A deck's output volume: channel fader x crossfade gain, applied to the deck's mixer.</summary>
public interface IDeckGain
{
    /// <summary>Raised with <see cref="DeckChange.Volume"/> whenever the mixer volume is applied and
    /// <see cref="DeckChange.ChannelGain"/> when the channel fader changes.</summary>
    event Action<DeckChange>? Changed;

    /// <summary>Channel fader 0..1, or null until measured. Unmeasured plays at unity.</summary>
    double? ChannelGain { get; set; }

    /// <summary>False until the channel fader has been measured.</summary>
    bool GainKnown { get; }

    /// <summary>Channel x crossfade gain, 0 when unmeasured.</summary>
    double EffectiveGain { get; }

    /// <summary>Measured and effectively silent.</summary>
    bool IsMuted { get; }

    /// <summary>Set when the crossfader moves.</summary>
    void SetCrossfadeGain(float gain);
}
