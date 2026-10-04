namespace Sholto.App.Mixer;

/// <summary>The mixer-wide state: the crossfader. One of the real roles extracted from the old
/// <c>IDeckHost</c>; <see cref="MixerSession"/> implements it. (Per-deck channel faders, EQ and filters are
/// the deck's own.)</summary>
public interface IMixer
{
    /// <summary>0..1, 0 = full deck 1, 1 = full deck 2. Setting it applies the equal-power gains to both decks.</summary>
    double Crossfader { get; set; }

    /// <summary>Raised after <see cref="Crossfader"/> was set, from whichever interface set it.</summary>
    event Action? CrossfaderChanged;
}
