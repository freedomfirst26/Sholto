using Sholto.App.Audio;
using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>See <see cref="IDeckStemMix"/>. The mute setters publish but do not touch the audio (the pad
/// handler pushes the mute itself); the level setters push to the audio.</summary>
public sealed class DeckStemMix(int index, IStemControl stems, IEventPublisher publisher) : IDeckStemMix
{
    private readonly int _index = index;
    private readonly IStemControl _stems = stems;
    private readonly IEventPublisher _publisher = publisher;

    private bool _vocalsActive = true, _instrumentalActive = true, _drumsActive = true;
    private double _drumsLevel = 1.0, _vocalsLevel = 1.0, _instrumentalLevel = 1.0;

    public event Action<DeckChange>? Changed;

    // Per-stem mute toggles. Default ON so a freshly-loaded track shows all three filled. Each change is
    // published for the controller's stem pads.
    public bool VocalsActive
    {
        get => _vocalsActive;
        set
        {
            if (_vocalsActive == value) return;
            _vocalsActive = value;
            Changed?.Invoke(DeckChange.VocalsActive);
            PublishStem(1);
        }
    }

    public bool InstrumentalActive
    {
        get => _instrumentalActive;
        set
        {
            if (_instrumentalActive == value) return;
            _instrumentalActive = value;
            Changed?.Invoke(DeckChange.InstrumentalActive);
            PublishStem(2);
        }
    }

    public bool DrumsActive
    {
        get => _drumsActive;
        set
        {
            if (_drumsActive == value) return;
            _drumsActive = value;
            Changed?.Invoke(DeckChange.DrumsActive);
            PublishStem(0);
        }
    }

    // Per-stem continuous level (0..1.5), driven by Shift + EQ knobs on the FLX-4. Default 1.0 (unity).
    // The setter pushes the level to the audio path.
    public double DrumsLevel
    {
        get => _drumsLevel;
        set
        {
            if (Math.Abs(_drumsLevel - value) < 1e-4) return;
            _drumsLevel = value;
            _stems.SetStemGroupLevel(0, value);
            Changed?.Invoke(DeckChange.DrumsLevel);
        }
    }

    public double VocalsLevel
    {
        get => _vocalsLevel;
        set
        {
            if (Math.Abs(_vocalsLevel - value) < 1e-4) return;
            _vocalsLevel = value;
            _stems.SetStemGroupLevel(1, value);
            Changed?.Invoke(DeckChange.VocalsLevel);
        }
    }

    public double InstrumentalLevel
    {
        get => _instrumentalLevel;
        set
        {
            if (Math.Abs(_instrumentalLevel - value) < 1e-4) return;
            _instrumentalLevel = value;
            _stems.SetStemGroupLevel(2, value);
            Changed?.Invoke(DeckChange.InstrumentalLevel);
        }
    }

    public void ResetForNewTrack()
    {
        DrumsActive = true;
        VocalsActive = true;
        InstrumentalActive = true;
        DrumsLevel = 1.0;
        VocalsLevel = 1.0;
        InstrumentalLevel = 1.0;
    }

    public void PublishCurrent()
    {
        for (var stem = 0; stem < StemMuteChanged.StemsPerDeck; stem++) PublishStem(stem);
    }

    private void PublishStem(int stem)
    {
        var active = stem switch
        {
            0 => _drumsActive,
            1 => _vocalsActive,
            _ => _instrumentalActive,
        };
        _publisher.Publish(new StemMuteChanged(_index, stem, !active));
    }
}
