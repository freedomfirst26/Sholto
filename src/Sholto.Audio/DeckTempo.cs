namespace Sholto.Audio;

/// <summary>
/// Pure tempo-fader model, extracted out of <see cref="Deck"/>. TempoRange is
/// the ± range the fader spans (0.06 = ±6%); TempoPosition is the fader
/// position 0..1 (0.5 = no shift). Effective playback speed =
/// 1 + (TempoPosition - 0.5) * 2 * TempoRange, compounded with BpmMultiplier.
/// Pioneer convention: position 0.0 = top of fader = slower (negative shift),
/// position 1.0 = bottom = faster — inverted here so "higher position =
/// faster" matches the visual intuition of moving the fader down.
///
/// This class only computes <see cref="PlaybackSpeed"/> and announces changes
/// via <see cref="PlaybackSpeedChanged"/> — it takes no collaborators and
/// pushes nothing anywhere itself. Deciding where that speed goes is
/// arbitration, not tempo: tempo and a platter scratch both ultimately want to
/// drive the same <c>IVarispeedProvider.SetSpeed</c>, last-writer-wins, so
/// something has to decide who wins when — and that job belongs to whoever
/// owns the provider and the scratch state, i.e. <see cref="Deck"/> (see its
/// field doc, and <see cref="Deck.ScratchRate"/> / <see cref="Deck.EndScratch"/>
/// for the hand-back rule: a scratch owns the provider's speed until
/// EndScratch gives it back).
/// </summary>
internal sealed class DeckTempo : IDeckTempo
{
    /// <summary>Raised every time <see cref="PlaybackSpeed"/> changes, carrying
    /// the new value. The owner decides what to do with it (Deck pushes it to
    /// the live varispeed provider, skipping while a scratch is in flight).</summary>
    public event Action<float>? PlaybackSpeedChanged;

    private double _tempoRange = 0.06;          // ±6% default
    private double _tempoPosition = 0.5;        // centred = unity speed
    private double _bpmMultiplier = 1.0;        // ½ / ×2 audio multiplier from BPM click

    /// <inheritdoc/>
    public double TempoRange
    {
        get => _tempoRange;
        set { _tempoRange = Math.Max(0, value); RecomputePlaybackSpeed(); }
    }

    /// <inheritdoc/>
    public double TempoPosition
    {
        get => _tempoPosition;
        set { _tempoPosition = Math.Clamp(value, 0, 1); RecomputePlaybackSpeed(); }
    }

    /// <inheritdoc/>
    public double BpmMultiplier
    {
        get => _bpmMultiplier;
        set { _bpmMultiplier = value > 0 ? value : 1.0; RecomputePlaybackSpeed(); }
    }

    /// <inheritdoc/>
    public float PlaybackSpeed { get; private set; } = 1.0f;

    private void RecomputePlaybackSpeed()
    {
        // Top of fader (pos=0) → slowdown, bottom (pos=1) → speedup.
        // (-1 + 2 * pos) maps 0..1 → -1..+1, then scaled by range.
        // Multiplied by BpmMultiplier so a halved track plays at half speed.
        double fader = 1.0 + (-1.0 + 2.0 * _tempoPosition) * _tempoRange;
        PlaybackSpeed = (float)(fader * _bpmMultiplier);
        PlaybackSpeedChanged?.Invoke(PlaybackSpeed);
    }
}
