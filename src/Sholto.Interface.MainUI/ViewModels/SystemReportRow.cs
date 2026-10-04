using System.Diagnostics.CodeAnalysis;
using Sholto.App.ExternalTools;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>
/// One line of the system report overlay: an external tool, whether the boot-time
/// probe found it, and — when it didn't — what is lost and how to get it back.
///
/// Built from a <see cref="ToolPresence"/>; it re-probes nothing. The cost/install
/// text is looked up by tool name in the constructor, which is the ONLY place in
/// the UI that knows those strings: madmom's install line is taken verbatim from
/// <see cref="MadmomBeatAnalysisStep.InstallCommand"/> rather than copied, and the
/// other two point at the project's installer because no equivalent constant exists
/// for them (the pinned demucs spec lives in <c>sholto-deps.sh</c>, not in C#) — a
/// second hand-written command here would be free to drift from the pin.
/// </summary>
public sealed class SystemReportRow
{
    public required string Capability { get; init; }
    public required string ToolName { get; init; }
    public required bool IsPresent { get; init; }

    /// <summary>"Found" + path when present; what is lost when absent.</summary>
    public required string Detail { get; init; }

    /// <summary>How to install it — empty string when the tool is present.</summary>
    public required string Install { get; init; }

    public string StatusText => IsPresent ? "installed" : "missing";
    public bool IsMissing => !IsPresent;

    [SetsRequiredMembers]
    public SystemReportRow(ToolPresence tool)
    {
        Capability = tool.Capability;
        ToolName = tool.ToolName;
        IsPresent = tool.IsPresent;
        Detail = tool.IsPresent ? (tool.BinaryPath ?? "") : CostOf(tool.ToolName);
        Install = tool.IsPresent ? "" : InstallOf(tool.ToolName);
    }

    private string CostOf(string toolName) => toolName switch
    {
        // Same wording as the boot-time console warning in SholtoStackFactory.Build.
        ExternalToolNames.Madmom =>
            "No BPM, beatgrid, waveform or key for any track.",
        ExternalToolNames.Demucs =>
            "No stem separation — no stem EQ, stem mutes or vocal regions.",
        ExternalToolNames.Ffmpeg =>
            "No M4A/AAC playback (MP3, FLAC and WAV still work).",
        _ => "Unavailable.",
    };

    private string InstallOf(string toolName) => toolName switch
    {
        ExternalToolNames.Madmom => MadmomBeatAnalysisStep.InstallCommand,
        _ => "Run ./install.sh from the Sholto folder, then restart Sholto.",
    };
}
