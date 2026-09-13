using SoundFlow.Enums;

namespace Sholto.Audio;

/// <summary>
/// Play/pause/seek, extracted out of <see cref="Deck"/>. The one piece of
/// state this needs — the live <c>SoundPlayer</c> — is rebuilt on every
/// Load/LoadStreaming/SwitchToStemMode and read by several other clusters
/// (scratch, loading) that stay on <see cref="Deck"/> in this stage, so the
/// field itself stays put; this component takes it as a narrow accessor
/// delegate instead of a back-reference to Deck, same as stages 1 and 2.
///
/// <see cref="SeekRelative"/> and <see cref="SeekToFraction"/> compute their
/// target from the provider's true cursor (<paramref
/// name="positionFrames">the position accessor</paramref>), not
/// <c>SoundPlayer.Time</c> — see <see cref="Deck.PositionFrames"/> for why
/// (varispeed/scratch drift the player's own clock away from the real
/// position). <see cref="Deck.PositionFrames"/> stays on Deck, so it's
/// likewise handed in as an accessor rather than recomputed here.
/// </summary>
internal sealed class TransportControl : ITransportControl
{
    private readonly Func<SoundFlow.Components.SoundPlayer?> _player;
    private readonly Func<long> _positionFrames;

    public TransportControl(Func<SoundFlow.Components.SoundPlayer?> player, Func<long> positionFrames)
    {
        _player = player;
        _positionFrames = positionFrames;
    }

    /// <inheritdoc/>
    public void Play() => _player()?.Play();

    /// <inheritdoc/>
    public void Pause() => _player()?.Pause();

    /// <inheritdoc/>
    public void TogglePlay()
    {
        var player = _player();
        if (player is null) { Console.WriteLine("[Deck] TogglePlay but no track loaded"); return; }
        if (player.State == PlaybackState.Playing) player.Pause(); else player.Play();
    }

    /// <inheritdoc/>
    public void SeekRelative(double seconds)
    {
        var player = _player();
        if (player is null) return;
        double current = _positionFrames() / (double)AudioFileDecoder.TargetSampleRate;
        double target = Math.Clamp(current + seconds, 0.0, player.Duration);
        player.Seek(TimeSpan.FromSeconds(target));
    }

    /// <inheritdoc/>
    public void SeekToFraction(double fraction)
    {
        var player = _player();
        if (player is null) return;
        double target = Math.Clamp(fraction, 0.0, 1.0) * player.Duration;
        player.Seek(TimeSpan.FromSeconds(target));
    }
}
