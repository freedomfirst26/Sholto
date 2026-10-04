using Sholto.App.Dsp;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Mixer;

/// <summary>See <see cref="IMixer"/>.</summary>
public sealed class MixerSession(IDecks decks, ICrossfadeCurve crossfade, IEventPublisher publisher) : IMixer
{
    private readonly IDecks _decks = decks;
    private readonly ICrossfadeCurve _crossfade = crossfade;
    private readonly IEventPublisher _publisher = publisher;
    private double _crossfader = 0.5;

    public event Action? CrossfaderChanged;

    public double Crossfader
    {
        get => _crossfader;
        set
        {
            _crossfader = Math.Clamp(value, 0.0, 1.0);
            // Equal-power crossfade: cosine curve so perceived loudness stays flat
            // through the centre. Each deck combines this with its own channel-fader gain.
            _crossfade.ComputeGains(_crossfader, out float gainA, out float gainB);
            _decks.Deck1.SetCrossfadeGain(gainA);
            _decks.Deck2.SetCrossfadeGain(gainB);
            CrossfaderChanged?.Invoke();
            _publisher.Publish(new Sholto.Data.CrossfaderChanged(_crossfader));
        }
    }
}
