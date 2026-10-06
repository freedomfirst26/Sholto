using System.Diagnostics.CodeAnalysis;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>
/// One line of the system report overlay: an external tool, whether the boot-time
/// probe found it, and — when it didn't — what is lost and how to get it back.
///
/// Built from a <see cref="ToolStatus"/>; it re-probes nothing. The cost text is
/// looked up by capability in the constructor, which is the ONLY place in the UI that
/// knows those strings. The install line is the status's own
/// <see cref="ToolStatus.InstallCommand"/> (madmom's, taken verbatim by the App side);
/// a tool without one points at the project's installer because no equivalent
/// constant exists for it (the pinned demucs spec lives in <c>sholto-deps.sh</c>, not
/// in C#) — a second hand-written command here would be free to drift from the pin.
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
    public SystemReportRow(ToolStatus tool)
    {
        Capability = tool.Capability;
        ToolName = tool.ToolName;
        IsPresent = tool.IsPresent;
        Detail = tool.IsPresent ? (tool.BinaryPath ?? "") : CostOf(tool.Capability);
        Install = tool.IsPresent ? "" : tool.InstallCommand ?? "Run ./install.sh from the Sholto folder, then restart Sholto.";
    }

    private string CostOf(string capability) => capability switch
    {
        // Same wording as the boot-time console warning in SholtoStackFactory.Build.
        ToolCapabilities.Beats =>
            "No BPM, beatgrid, waveform or key for any track.",
        ToolCapabilities.Stems =>
            "No stem separation — no stem EQ, stem mutes or vocal regions.",
        ToolCapabilities.Transcode =>
            "No M4A/AAC playback (MP3, FLAC and WAV still work).",
        _ => "Unavailable.",
    };
}
