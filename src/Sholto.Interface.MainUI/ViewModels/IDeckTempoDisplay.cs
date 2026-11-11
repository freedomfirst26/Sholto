using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The BPM and tempo readouts of one deck, derived from the last <see cref="DeckTempoChanged"/>.</summary>
public interface IDeckTempoDisplay
{
    void Apply(in DeckTempoChanged e);

    /// <summary>Source BPM (from analysis), unscaled.</summary>
    double SourceBpm { get; }

    /// <summary>User-applied multiplier (half / double).</summary>
    double BpmMultiplier { get; }

    /// <summary>Source BPM x user multiplier x current playback speed: what the user actually hears.</summary>
    double EffectiveBpm { get; }

    bool HasBpm { get; }

    /// <summary>True only when the tempo FADER has moved off-centre.</summary>
    bool IsTempoShifted { get; }

    bool WasMagnetAdjusted { get; }

    /// <summary>Should the "original BPM" side chip be popped out? True when the fader is meaningfully
    /// shifted OR when magnet snap just adjusted us.</summary>
    bool ShowOriginalBpm { get; }

    string BpmDisplay { get; }

    /// <summary>Just the number, no "BPM" suffix: used inside the disc.</summary>
    string BpmDisplayShort { get; }

    /// <summary>"Original" = source x half/double override, but without the live tempo-fader shift.</summary>
    string OriginalBpmDisplay { get; }

    string OriginalBpmShort { get; }

    /// <summary>"+-6%" / "+-10%" / "+-16%" / "WIDE": the current tempo-range mode.</summary>
    string TempoRangeDisplay { get; }
}
