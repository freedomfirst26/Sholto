namespace Sholto.App;

/// <summary>Tunable settings for the magnetic beat-snap, supplied via the standard
/// <c>IOptions&lt;MagnetismOptions&gt;</c> pipeline. Defaults live here; a config
/// source can override them later without touching the view model.</summary>
public sealed class MagnetismOptions
{
    /// <summary>How close the two decks' tempos must be, as a fraction (0.01 = 1%),
    /// for the magnet to engage. At/under this the beat-snap locks tempo + phase;
    /// over it the decks are treated as un-matched and the magnet stays off. 0.5%
    /// was rarely reachable by hand (~0.9 BPM at 174); 1% covers a real rough
    /// beat-match while keeping the tempo nudge on engage small.</summary>
    public double BpmEligibilityTolerance { get; set; } = 0.01;

    /// <summary>The jog must have been idle this long before auto-quantize reads it as
    /// "the user let go".</summary>
    public TimeSpan JogIdleForQuantize { get; set; } = TimeSpan.FromMilliseconds(180);

    /// <summary>Auto-quantize only counts as "user released a jog gesture" if the jog was
    /// this recent. Without the window, two decks at different tempos would drift into
    /// alignment and an old jog from minutes ago would trigger a surprise seek.</summary>
    public TimeSpan JogRecencyForQuantize { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Alignment factor (0..1, 1 = downbeats exactly together) at or over which the
    /// magnet engages: the downbeat glow shows and, once the user lets go of the jog, the snap
    /// fires. The same value drives both, so "see one, fire one".</summary>
    public double EngageThreshold { get; set; } = 0.3;

    /// <summary>Alignment factor under which the magnet re-arms after a snap. Lower than
    /// <see cref="EngageThreshold"/> on purpose: the hysteresis stops it re-firing as the
    /// factor chatters around the engage level.</summary>
    public double DisengageThreshold { get; set; } = 0.15;

    /// <summary>Downbeat misalignment, in seconds, at which the alignment factor falls to 0
    /// (smoothstep from 1 at 0 s). 150 ms: bar-start tolerance is wider than beat-start.</summary>
    public double BarWindowSeconds { get; set; } = 0.15;
}
