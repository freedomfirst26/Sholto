namespace Sholto.App.Decks;

/// <summary>One deck's tempo: the half/double BPM multiplier, the tempo fader and its range, and whether the
/// magnet snap retuned it. Raises <see cref="Changed"/> with <see cref="DeckChange.BpmMultiplier"/>,
/// <see cref="DeckChange.Tempo"/>, <see cref="DeckChange.TempoRange"/> or
/// <see cref="DeckChange.MagnetAdjusted"/>.</summary>
public interface IDeckTempoControl
{
    event Action<DeckChange>? Changed;

    /// <summary>Raised when the user chose a multiplier (toggle, halve, double, reset), with the new value,
    /// so the owner can persist it. Not raised by <see cref="RestoreMultiplier"/>.</summary>
    event Action<double>? BpmMultiplierChosen;

    double SourceBpm { get; }

    /// <summary>User-applied multiplier: drives BOTH the displayed BPM and the playback speed of the deck.</summary>
    double BpmMultiplier { get; }

    /// <summary>Source BPM x user multiplier x current playback speed: what the user actually hears.</summary>
    double EffectiveBpm { get; }

    /// <summary>True only when the tempo FADER has moved off-centre.</summary>
    bool IsTempoShifted { get; }

    /// <summary>True when the magnet snap retuned this deck's tempo to match its partner.</summary>
    bool WasMagnetAdjusted { get; }

    void ToggleBpmOverride();
    void HalveBpm();
    void DoubleBpm();
    void ResetBpmMultiplier();

    /// <summary>Apply a multiplier that came from disk, not the user: pushes it to the audio and raises
    /// <see cref="DeckChange.BpmMultiplier"/> even when unchanged, but does not raise
    /// <see cref="BpmMultiplierChosen"/>.</summary>
    void RestoreMultiplier(double multiplier);

    void ClearMagnet();

    bool MatchEffectiveBpm(double targetBpm);
    void SetTempoPosition(double pos);
    void SetTempoRange(double range);
    void CycleTempoRange();
}
