namespace Sholto.App.Audio;

/// <summary>
/// A deck's platter scratch: owns the two scratch flags and drives the shared
/// <see cref="IVarispeedProvider"/> from platter velocity. Deck arbitrates tempo
/// against it through <see cref="IsScratching"/>. Runs on the app thread; no
/// locks, and the accessor delegates are built once by Deck.
/// </summary>
internal sealed class DeckScratch(
    Func<IVarispeedProvider?> varispeed,
    Func<SoundFlow.Components.SoundPlayer?> player,
    Func<bool> isPlaying,
    Func<float> playbackSpeed,
    Func<long> positionFrames) : IDeckScratchControl
{
    private readonly Func<IVarispeedProvider?> _varispeed = varispeed;
    private readonly Func<SoundFlow.Components.SoundPlayer?> _player = player;
    private readonly Func<bool> _isPlaying = isPlaying;
    private readonly Func<float> _playbackSpeed = playbackSpeed;
    private readonly Func<long> _positionFrames = positionFrames;

    // While active, the deck's effective playback rate is driven directly by
    // Orchestrator (platter velocity, signed, any magnitude) instead of by
    // PlaybackSpeed. See ScratchRate / EndScratch below.
    private bool _scratching;
    // Whether the deck was actually Playing (per the user, before we may have
    // force-started it below) at the moment the platter was first grabbed.
    // EndScratch restores this rather than assuming "playing".
    private bool _wasPlayingBeforeScratch;

    public bool IsScratching => _scratching;

    /// <summary>True once the deck is on a provider that supports signed
    /// vinyl-style speed (in-memory raw buffer or stem mix) — i.e. everywhere
    /// except the very first moment of a streamed load, before <see cref="Deck.Load"/>
    /// has swapped in a real provider. Orchestrator checks this before routing
    /// a top-platter tick into <see cref="ScratchRate"/>; when false it falls
    /// back to the old silent-seek jog behaviour.</summary>
    public bool CanScratch => _varispeed() is not null;

    /// <summary>Drive the deck's playback rate directly from platter velocity:
    /// <paramref name="rate"/> is a signed multiple of unity (1.0 = normal
    /// forward speed, 0 = held, negative = reverse), REPLACING — not
    /// composing with — the tempo-fader-derived <see cref="Deck.PlaybackSpeed"/>
    /// while a scratch is in flight. No-op if the deck isn't on a scratchable
    /// provider (see <see cref="CanScratch"/>).
    /// <para>SoundFlow's SoundPlayer only pulls samples from the data provider
    /// while its internal state is <c>Playing</c> (a paused player renders
    /// silence without even calling ReadBytes) — so a deck that was paused
    /// when the platter was grabbed is force-started here. That matches CDJ
    /// vinyl mode: the platter drives sound even parked, and at rate 0
    /// ReadBytes just re-emits the frame under the needle (near-silent hold,
    /// not literal silence). <see cref="EndScratch"/> restores whatever
    /// play/pause state the deck was actually in.</para></summary>
    public void ScratchRate(double rate)
    {
        var vp = _varispeed();
        var player = _player();
        if (vp is null || player is null) return;
        if (!_scratching)
        {
            _scratching = true;
            _wasPlayingBeforeScratch = _isPlaying();
            if (!_wasPlayingBeforeScratch) player.Play();
        }
        vp.SetSpeed((float)rate);
    }

    /// <summary>Release the platter: hand the provider's speed back to the
    /// deck's own <see cref="Deck.PlaybackSpeed"/> (tempo fader × BPM multiplier),
    /// and restore whichever play/pause state the deck was in before the
    /// scratch began. No-op if no scratch is in flight.</summary>
    public void EndScratch()
    {
        if (!_scratching) return;
        _scratching = false;
        _varispeed()?.SetSpeed(_playbackSpeed());
        // Resync the SoundPlayer's INTERNAL clock to where the scratch actually
        // left the provider. The player's _rawSamplePosition only advances by
        // frames rendered in real time — a scratch moves the provider's cursor
        // far beyond/behind that, so player.Time goes stale at roughly the
        // pre-scratch position. SeekRelative/SeekToFraction compute their targets
        // from player.Time, so the FIRST post-scratch seek (side-ring nudge,
        // quantize, minimap click) would yank playback back to where the scratch
        // began. A same-place Seek here re-bases both clocks onto the provider's
        // true position (the providers' 256-frame declick fade makes it silent).
        var player = _player();
        if (player is not null)
            player.Seek(TimeSpan.FromSeconds(
                _positionFrames() / (double)AudioFileDecoder.TargetSampleRate));
        if (!_wasPlayingBeforeScratch) player?.Pause();
    }

    public void ResetForNewTrack() => _scratching = false;
}
