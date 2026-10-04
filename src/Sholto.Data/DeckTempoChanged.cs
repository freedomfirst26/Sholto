namespace Sholto.Data;

/// <summary>A deck's tempo picture: detected BPM, the half/double multiplier, the BPM the user hears, the playback speed, the tempo-fader range (0.06, 0.10, 0.16, 1.0 = WIDE) and the two flags the BPM chip follows. State.</summary>
public readonly record struct DeckTempoChanged(int Deck, double SourceBpm, double BpmMultiplier, double EffectiveBpm, double PlaybackSpeed, double TempoRange, bool IsTempoShifted, bool WasMagnetAdjusted) : IStateEvent
{
    public int Slot => Deck;
}
