namespace Sholto.App.ExternalTools;

/// <summary>How much of the app works on this machine, derived from the boot-time
/// tool probe. <see cref="Degraded"/> and <see cref="Offline"/> both render as one
/// amber dot in the UI — the distinction is what the system report says, not the
/// colour.</summary>
public enum SystemStatus
{
    /// <summary>Every capability has at least one of its tools resolved.</summary>
    Healthy,

    /// <summary>Every required capability is backed, but at least one optional capability
    /// has no tool — the app runs, that capability is simply absent.</summary>
    Degraded,

    /// <summary>A required capability has no tool (neither Beat This! nor its madmom
    /// fallback for beats): no beats, grid, BPM, waveform or key for any track.</summary>
    Offline,
}
