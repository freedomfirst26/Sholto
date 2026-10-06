using Sholto.App.Audio;

namespace Sholto.App.Decks;

/// <summary>One deck's tempo: BPM multiplier, tempo fader and range, magnet-adjusted flag.</summary>
public sealed class DeckTempoControl(ITrackLoading loading, IDeckTempo tempo) : IDeckTempoControl
{
    // Rekordbox-style tempo-range stops: +-6 -> +-10 -> +-16 -> WIDE (+-100). The fader keeps its physical
    // position; only the span it maps to changes, so the current playback speed is preserved across a
    // range change as long as TempoPosition is re-applied (the TempoRange setter does this).
    private readonly double[] _tempoRangeStops = [0.06, 0.10, 0.16, 1.00];

    private readonly ITrackLoading _loading = loading;
    private readonly IDeckTempo _tempo = tempo;

    private double _bpmMultiplier = 1.0;
    private bool _wasMagnetAdjusted;

    public event Action<DeckChange>? Changed;
    public event Action<double>? BpmMultiplierChosen;

    public double SourceBpm => _loading.Analysis.Basic?.Bpm ?? 0;

    /// <summary>User-applied multiplier: drives BOTH the displayed BPM and the playback speed of the deck.
    /// Half slows the track to match a corrected reading, double the opposite.</summary>
    public double BpmMultiplier
    {
        get => _bpmMultiplier;
        private set
        {
            if (Math.Abs(_bpmMultiplier - value) < 0.0001) return;
            _bpmMultiplier = value;
            _tempo.BpmMultiplier = value;   // also halve/double the actual audio
            Changed?.Invoke(DeckChange.BpmMultiplier);
        }
    }

    private void SetMultiplierAndPersist(double newValue)
    {
        BpmMultiplier = newValue;
        BpmMultiplierChosen?.Invoke(newValue);
    }

    /// <summary>One-click flip-flop. If already overridden, returns to original (multiplier = 1). If at
    /// original, picks the most-likely correction: high BPM halves, low BPM doubles.</summary>
    public void ToggleBpmOverride()
    {
        if (Math.Abs(BpmMultiplier - 1.0) > 0.001)
        {
            SetMultiplierAndPersist(1.0);
            return;
        }
        // At unity. Pick a direction from the source BPM the analyser found. A threshold of 120 catches the
        // common cases: anything >= 120 was likely doubled by madmom and gets halved; anything < 120 gets
        // doubled.
        SetMultiplierAndPersist(SourceBpm >= 120 ? 0.5 : 2.0);
    }

    public void HalveBpm() => SetMultiplierAndPersist(BpmMultiplier * 0.5);
    public void DoubleBpm() => SetMultiplierAndPersist(BpmMultiplier * 2.0);
    public void ResetBpmMultiplier() => SetMultiplierAndPersist(1.0);

    /// <summary>Apply a multiplier that came from disk, not the user, so <see cref="BpmMultiplierChosen"/>
    /// does not fire.</summary>
    public void RestoreMultiplier(double multiplier)
    {
        _bpmMultiplier = multiplier;
        _tempo.BpmMultiplier = multiplier;
        Changed?.Invoke(DeckChange.BpmMultiplier);
    }

    public void ClearMagnet() => WasMagnetAdjusted = false;

    /// <summary>Source BPM x user multiplier x current playback speed: what the user actually hears.</summary>
    public double EffectiveBpm => SourceBpm * _bpmMultiplier * _tempo.PlaybackSpeed;

    /// <summary>True only when the tempo FADER has moved off-centre. The half/double override doesn't
    /// trigger this: it is a correction to the analysed source, not a live performance shift.</summary>
    public bool IsTempoShifted => Math.Abs(_tempo.PlaybackSpeed - 1.0) > 0.002;

    /// <summary>True when the magnet snap retuned this deck's tempo to match its partner. Stays true until
    /// the user touches their tempo fader or a new track loads.</summary>
    public bool WasMagnetAdjusted
    {
        get => _wasMagnetAdjusted;
        private set
        {
            if (_wasMagnetAdjusted == value) return;
            _wasMagnetAdjusted = value;
            Changed?.Invoke(DeckChange.MagnetAdjusted);
        }
    }

    /// <summary>Adjust this deck's tempo fader so its <see cref="EffectiveBpm"/> matches
    /// <paramref name="targetBpm"/>. Used by magnet-snap: once two decks phase-align, locking their
    /// effective BPMs is what keeps them locked. If the required shift falls outside the tempo range,
    /// clamps to the edge of the fader range and gets as close as possible. Returns false on inputs that
    /// don't make sense (target &lt;= 0, no source BPM, no pitch range configured).</summary>
    public bool MatchEffectiveBpm(double targetBpm)
    {
        if (targetBpm <= 0) return false;
        double current = EffectiveBpm;
        if (current <= 0) return false;

        // Already at the target (within display precision): no shift needed and no chip-pop. The magnet
        // glyph already told the user they're locked; popping the OriginalBpm side chip here would be a
        // lie since the deck wasn't actually retuned.
        if (Math.Abs(targetBpm - current) < 0.01) return false;

        // Work in PlaybackSpeed-space to dodge any subtlety in how SourceBpm / BpmMultiplier compose.
        // EffectiveBpm scales linearly with PlaybackSpeed, so the ratio is what we need.
        double desiredPlaybackSpeed = _tempo.PlaybackSpeed * (targetBpm / current);

        double mult = _bpmMultiplier > 0 ? _bpmMultiplier : 1.0;
        double desiredFader = desiredPlaybackSpeed / mult;

        double range = _tempo.TempoRange;
        if (range <= 0) return false;

        // PlaybackSpeed fader = 1 + (-1 + 2*pos) * range  =>  pos = 0.5 + (fader-1)/(2*range).
        double pos = 0.5 + (desiredFader - 1.0) / (2.0 * range);
        ApplyTempoPosition(Math.Clamp(pos, 0.0, 1.0));
        // Flag this as a magnet-driven adjustment so the OriginalBpm chip pops out even though the actual
        // shift may be far below IsTempoShifted's 0.2 % threshold (a 176.5 -> 176.6 lock is only 0.06 %).
        WasMagnetAdjusted = true;
        return true;
    }

    /// <summary>Forward the FLX-4 tempo fader to the player. Position 0..1, 0.5 = unity. User-driven: also
    /// clears the magnet-adjusted flag, since touching the fader means "I'm taking control back."</summary>
    public void SetTempoPosition(double pos)
    {
        ApplyTempoPosition(pos);
        WasMagnetAdjusted = false;
    }

    /// <summary>Internal tempo-fader write that doesn't touch the magnet flag, so
    /// <see cref="MatchEffectiveBpm"/> can set <see cref="WasMagnetAdjusted"/> itself.</summary>
    private void ApplyTempoPosition(double pos)
    {
        _tempo.TempoPosition = pos;
        Changed?.Invoke(DeckChange.Tempo);
    }

    public void SetTempoRange(double range)
    {
        _tempo.TempoRange = range;
        Changed?.Invoke(DeckChange.TempoRange);
    }

    public void CycleTempoRange()
    {
        double current = _tempo.TempoRange;
        int idx = 0;
        for (int i = 0; i < _tempoRangeStops.Length; i++)
            if (Math.Abs(_tempoRangeStops[i] - current) < 1e-6) { idx = i; break; }
        double next = _tempoRangeStops[(idx + 1) % _tempoRangeStops.Length];
        SetTempoRange(next);
        Console.WriteLine($"[Deck] tempo range -> {(next >= 0.99 ? "WIDE" : $"+-{next * 100:F0}%")}");
    }
}
