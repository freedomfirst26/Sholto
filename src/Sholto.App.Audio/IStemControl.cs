namespace Sholto.App.Audio;

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

    /// <summary>Continuous stem-group attenuator driven by Shift
    /// + the EQ knobs (HI = drums, MID = vocals, LOW = inst). The
    /// level is the stem's gain (1.0 = unity; the controller maps knob
    /// centre to 1.0). A small bottom dead zone is hard-zero (≤0.08 → 0,
    /// 0.08–1.0 → linear ramp, ≥1.0 → unity / no boost). Independent from <see cref="SetStemGroup"/>: turning the knob
    /// while pad-muted updates the stored level silently; mute stays in
    /// effect. Unmuting then brings the stem back at the stored level.</summary>
    void SetStemGroupLevel(int group, double level);
}
