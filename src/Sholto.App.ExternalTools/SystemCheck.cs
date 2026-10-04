namespace Sholto.App.ExternalTools;

/// <summary>
/// Named, reportable form of the boot-time tool probe <see cref="ToolSet"/> already
/// runs once in its constructor. Built purely from <see cref="ToolSet"/>'s resolved
/// paths (see <see cref="ToolSet.Check"/>) — this type does no resolution of its own.
/// </summary>
public sealed record SystemCheck(IReadOnlyList<ToolPresence> Tools)
{
    public bool AnyRequiredMissing => Tools.Any(t => t.Required && !t.IsPresent);

    /// <summary>Severity of this check — required-missing outranks optional-missing.
    /// Read by the UI's status dot and its system report; derived here rather than in
    /// a view model so both the console line and the dot agree by construction.</summary>
    public SystemStatus Status =>
        AnyRequiredMissing ? SystemStatus.Offline
        : Tools.Any(t => !t.IsPresent) ? SystemStatus.Degraded
        : SystemStatus.Healthy;

    /// <summary>One line per tool, e.g. "beats/DBNDownBeatTracker OK (/path);
    /// stems/demucs MISSING; transcode/ffmpeg OK (/path)".</summary>
    public string Summary => string.Join("; ", Tools.Select(t =>
        t.IsPresent
            ? $"{t.Capability}/{t.ToolName} OK ({t.BinaryPath})"
            : $"{t.Capability}/{t.ToolName} MISSING"));
}
