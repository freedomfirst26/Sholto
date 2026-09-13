namespace Sholto.Audio;

/// <summary>
/// A deck's tempo-fader model: the ± range the fader spans, the fader's
/// own position, the half/double BPM-click multiplier, and the resulting live
/// playback-speed multiplier. See <see cref="DeckTempo"/> for the
/// implementation moved out of <c>Deck</c>.
///
/// "Tempo" and "pitch" are deliberately one concept here, not two: vinyl mode
/// means pitch shifts with speed, so there is only one knob. If key-lock /
/// master-tempo (speed without a pitch shift) is ever added they become
/// genuinely different things again, and these names — and this doc comment
/// — will need to split back apart with them.
/// </summary>
public interface IDeckTempo
{
    /// <summary>The ± range the tempo fader spans (0.06 = ±6%).</summary>
    double TempoRange { get; set; }

    /// <summary>0..1, 0.5 = no shift. The same value the FLX-4 fader sends.</summary>
    double TempoPosition { get; set; }

    /// <summary>Half / double / unity playback multiplier driven by the
    /// BPM-click override on the deck. Compounds with the live tempo fader.</summary>
    double BpmMultiplier { get; set; }

    /// <summary>Live playback-speed multiplier (1.0 = unity), already
    /// factoring in both the fader's ±range shift and the BPM-click
    /// override.</summary>
    float PlaybackSpeed { get; }
}
