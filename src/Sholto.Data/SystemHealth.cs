namespace Sholto.Data;

/// <summary>How much of the app works on this machine, derived from the boot-time tool probe.</summary>
public enum SystemHealth
{
    /// <summary>Every tool resolved.</summary>
    Healthy,

    /// <summary>Every required tool resolved, but at least one optional one did not.</summary>
    Degraded,

    /// <summary>A required tool is missing: no beats, grid, BPM, waveform or key for any track.</summary>
    Offline,
}
