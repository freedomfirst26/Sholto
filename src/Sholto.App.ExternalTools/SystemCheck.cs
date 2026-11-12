using Sholto.Data;

namespace Sholto.App.ExternalTools;

/// <summary>
/// Named, reportable form of the boot-time tool probe <see cref="ToolSet"/> already
/// runs once in its constructor. Built purely from <see cref="ToolSet"/>'s resolved
/// paths (see <see cref="ToolSet.Check"/>) — this type does no resolution of its own.
///
/// Severity is judged per capability, not per tool: a capability is covered when any
/// tool backing it is present. So beats are fine when Beat This! is missing but its
/// fallback madmom is there, and a missing fallback beside a working Beat This! costs
/// nothing.
/// </summary>
public sealed record SystemCheck(IReadOnlyList<ToolPresence> Tools)
{
    /// <summary>True when a required tool is missing and no present tool backs its capability.</summary>
    public bool AnyRequiredMissing => Tools.Any(t => t.Required && !IsCovered(t.Capability));

    /// <summary>Severity of this check — required-missing outranks optional-missing.
    /// Read by the UI's status dot and its system report; derived here rather than in
    /// a view model so both the console line and the dot agree by construction.</summary>
    public SystemStatus Status =>
        AnyRequiredMissing ? SystemStatus.Offline
        : Tools.Any(t => !IsCovered(t.Capability)) ? SystemStatus.Degraded
        : SystemStatus.Healthy;

    /// <summary>One line per tool, e.g. "beats/beat_this OK (/path);
    /// stems/demucs MISSING; transcode/ffmpeg OK (/path)".</summary>
    public string Summary => string.Join("; ", Tools.Select(t =>
        t.IsPresent
            ? $"{t.Capability}/{t.ToolName} OK ({t.BinaryPath})"
            : $"{t.Capability}/{t.ToolName} MISSING"));

    /// <summary>This check as the event the interfaces are told: every tool, its install command where one is
    /// known (Beat This!'s and madmom's), and <see cref="Status"/> as a <see cref="SystemHealth"/>.</summary>
    public SystemCheckReported ToReported() =>
        new(Tools.Select(t => new ToolStatus(
                t.ToolName, t.Capability, t.Required, t.BinaryPath, InstallCommandFor(t.ToolName))).ToList(),
            Status switch
            {
                SystemStatus.Offline => SystemHealth.Offline,
                SystemStatus.Degraded => SystemHealth.Degraded,
                _ => SystemHealth.Healthy,
            });

    private bool IsCovered(string capability) => Tools.Any(t => t.Capability == capability && t.IsPresent);

    private string? InstallCommandFor(string toolName) => toolName switch
    {
        ExternalToolNames.BeatThis => BeatThisBeatAnalysisStep.InstallCommand,
        ExternalToolNames.Madmom => MadmomBeatAnalysisStep.InstallCommand,
        _ => null,
    };
}
