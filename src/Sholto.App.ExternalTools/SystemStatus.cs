namespace Sholto.App.ExternalTools;

/// <summary>How much of the app works on this machine, derived from the boot-time
/// tool probe. <see cref="Degraded"/> and <see cref="Offline"/> both render as one
/// amber dot in the UI — the distinction is what the system report says, not the
/// colour.</summary>
public enum SystemStatus
{
    /// <summary>Every tool resolved.</summary>
    Healthy,

    /// <summary>Every REQUIRED tool resolved, but at least one optional one did not —
    /// the app runs, some capability is simply absent.</summary>
    Degraded,

    /// <summary>A required tool is missing (madmom): no beats, grid, BPM, waveform or
    /// key for any track.</summary>
    Offline,
}
