using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

public sealed class DeckTempoDisplay : IDeckTempoDisplay
{
    private DeckTempoChanged _last = new(0, 0, 1.0, 0, 1.0, 0.06, false, false);

    public void Apply(in DeckTempoChanged e) => _last = e;

    public double SourceBpm => _last.SourceBpm;
    public double BpmMultiplier => _last.BpmMultiplier;
    public double EffectiveBpm => _last.EffectiveBpm;
    public bool HasBpm => SourceBpm > 0;
    public bool IsTempoShifted => _last.IsTempoShifted;
    public bool WasMagnetAdjusted => _last.WasMagnetAdjusted;
    public bool ShowOriginalBpm => IsTempoShifted || WasMagnetAdjusted;

    public string BpmDisplay =>
        SourceBpm > 0 ? $"{EffectiveBpm:F1} BPM" : "";

    public string BpmDisplayShort =>
        SourceBpm > 0 ? $"{EffectiveBpm:F1}" : "";

    public string OriginalBpmDisplay =>
        SourceBpm > 0 ? $"{(SourceBpm * BpmMultiplier):F1} BPM" : "";

    public string OriginalBpmShort =>
        SourceBpm > 0 ? $"{(SourceBpm * BpmMultiplier):F1}" : "";

    public string TempoRangeDisplay =>
        _last.TempoRange >= 0.99 ? "WIDE" : $"±{_last.TempoRange * 100:F0}%";
}
