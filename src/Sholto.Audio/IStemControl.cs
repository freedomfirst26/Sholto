namespace Sholto.Audio;

/// <summary>
/// The 3 UI stem groups (drums / vocals / instrumental): mute and continuous
/// level, each independent of the other. Extracted out of <see cref="Deck"/> —
/// see <see cref="StemControl"/> for the implementation and why it takes no
/// back-reference to Deck.
/// </summary>
public interface IStemControl
{
    /// <summary>Mute/unmute one of the 3 UI groups (drums / vocals / instrumental).
    /// Independent from <see cref="SetStemGroupLevel"/>: gain = active × level.
    /// Lock-free.</summary>
    void SetStemGroup(int group, bool active);

    /// <summary>Continuous stem-group attenuator driven by the StemLevelMode
    /// modifier + EQ knobs (HI = drums, MID = vocals, LOW = inst). Knob
    /// position (0..1) maps through a Pioneer-style "isolator kill" curve so
    /// the bottom half does all the attenuation and the bottom detent is
    /// hard-zero (≤0.04 → 0, 0.04–0.5 → linear ramp, ≥0.5 → unity / no
    /// boost). Independent from <see cref="SetStemGroup"/>: turning the knob
    /// while pad-muted updates the stored level silently; mute stays in
    /// effect. Unmuting then brings the stem back at the stored level.</summary>
    void SetStemGroupLevel(int group, double level);
}
