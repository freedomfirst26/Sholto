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
}
