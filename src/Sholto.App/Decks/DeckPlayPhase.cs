using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>See <see cref="IDeckPlayPhase"/>.</summary>
public sealed class DeckPlayPhase(int index, IFrameClock clock, IEventPublisher publisher) : IDeckPlayPhase
{
    /// <summary>Half of the end-of-track flash period: 400 ms lit, 400 ms dark (800 ms period, the old
    /// disc-ring rhythm).</summary>
    private const double FlashHalfPeriodMs = 400;

    private const double NearEndPosition = 0.9;

    private readonly int _index = index;
    private readonly IFrameClock _clock = clock;
    private readonly IEventPublisher _publisher = publisher;

    private bool _isPlaying;
    private double _playPosition;
    private PlayPhase _lastPlayState = PlayPhase.Stopped;
    private bool _flashOn;
    private DateTime _endingSince;

    public event Action<DeckChange>? Changed;

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value) return;
            _isPlaying = value;
            Changed?.Invoke(DeckChange.IsPlaying);
            RefreshPlayState();
        }
    }

    public PlayPhase PlayState =>
        !_isPlaying ? PlayPhase.Stopped :
        IsNearEnd   ? PlayPhase.Ending :
                      PlayPhase.Playing;

    private bool IsNearEnd => _playPosition >= NearEndPosition;

    public bool EndFlashOn => _lastPlayState == PlayPhase.Ending && _flashOn;

    public double PlayPosition
    {
        get => _playPosition;
        set
        {
            _playPosition = value;
            Changed?.Invoke(DeckChange.PlayPosition);
            // Position can cross the near-end threshold without any play/pause event, so re-derive the
            // transport state here too (Playing -> Ending).
            RefreshPlayState();
        }
    }

    /// <summary>Recompute the play phase after a trigger (play/pause OR position crossing the near-end
    /// threshold). On a change: stamp the moment Ending began (the flash phase counts from it), and tell the
    /// view model and the bus.</summary>
    private void RefreshPlayState()
    {
        var state = PlayState;
        if (state == _lastPlayState) return;
        _lastPlayState = state;

        if (state == PlayPhase.Ending)
        {
            _endingSince = _clock.Now;
            _flashOn = true;
        }
        else
        {
            _flashOn = false;
        }

        Changed?.Invoke(DeckChange.PlayState);
        PublishCurrent();
    }

    /// <summary>The single end-of-track flash: lit for the first <see cref="FlashHalfPeriodMs"/> after
    /// Ending began, then alternating. The phase is a pure function of the frame clock, so the controller
    /// light and the disc ring (both fed from here) cannot drift apart. Allocation-free.</summary>
    public void UpdateFlash()
    {
        if (_lastPlayState != PlayPhase.Ending) return;
        var elapsedMs = Math.Max(0, (_clock.Now - _endingSince).TotalMilliseconds);
        var on = ((long)(elapsedMs / FlashHalfPeriodMs) & 1L) == 0;
        if (on == _flashOn) return;
        _flashOn = on;
        Changed?.Invoke(DeckChange.EndFlash);
        PublishCurrent();
    }

    public void PublishCurrent() =>
        _publisher.Publish(new DeckPlayStateChanged(_index, _lastPlayState, EndFlashOn));
}
