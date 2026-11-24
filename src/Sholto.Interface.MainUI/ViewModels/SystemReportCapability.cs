using System.Diagnostics.CodeAnalysis;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>
/// One card of the system report overlay: a capability (beats, stems, transcode), whether the boot-time probe
/// found a tool that backs it, and — when it did not — what is lost and how to get it back.
///
/// Built from the <see cref="ToolStatus"/>es that share the capability, in report order; the first is the
/// preferred tool, any later one a fallback. It re-probes nothing. The DJ label, the "what it gives" line and the
/// cost text are looked up by capability in the constructor, which is the ONLY place in the UI that knows those
/// strings. The install line is the preferred status's own <see cref="ToolStatus.InstallCommand"/>; a tool without
/// one points at the project's installer because no equivalent constant exists for it (the pinned demucs spec
/// lives in <c>sholto-deps.sh</c>, not in C#) — a second hand-written command here would be free to drift from it.
/// </summary>
public sealed class SystemReportCapability
{
    /// <summary>What the code box holds when a tool has no install command of its own.</summary>
    public const string InstallerCommand = "./install.sh";

    public required string Name { get; init; }
    public required SystemReportCapabilityState State { get; init; }
    public required string StatusText { get; init; }
    public required string Detail { get; init; }
    public required string ToolLine { get; init; }

    /// <summary>The sentence above the code box; empty when nothing needs fixing.</summary>
    public required string FixLabel { get; init; }

    /// <summary>Pasteable text for the code box; empty when nothing needs fixing.</summary>
    public required string FixCommand { get; init; }

    public bool IsReady => State != SystemReportCapabilityState.Missing;
    public bool IsMissing => State == SystemReportCapabilityState.Missing;
    public bool IsFallback => State == SystemReportCapabilityState.ReadyWithFallback;
    public bool HasFix => FixCommand.Length > 0;

    [SetsRequiredMembers]
    public SystemReportCapability(IReadOnlyList<ToolStatus> tools)
    {
        var preferred = tools[0];
        var present = tools.FirstOrDefault(t => t.IsPresent);
        var capability = preferred.Capability;
        var fix = preferred.InstallCommand ?? InstallerCommand;

        Name = NameOf(capability);
        if (present is null)
        {
            State = SystemReportCapabilityState.Missing;
            StatusText = "Not available";
            Detail = CostOf(capability);
            ToolLine = $"{preferred.ToolName} · not found";
            FixLabel = fix == InstallerCommand ? "To fix: run the installer from the Sholto folder." : "To fix:";
            FixCommand = fix;
        }
        else if (ReferenceEquals(present, preferred))
        {
            State = SystemReportCapabilityState.Ready;
            StatusText = "Ready";
            Detail = GivesOf(capability);
            ToolLine = $"{preferred.ToolName} · {preferred.BinaryPath}";
            FixLabel = "";
            FixCommand = "";
        }
        else
        {
            State = SystemReportCapabilityState.ReadyWithFallback;
            StatusText = "Ready";
            Detail = $"Working, using the {present.ToolName} fallback. {preferred.ToolName} is not installed.";
            ToolLine = $"{present.ToolName} · {present.BinaryPath}";
            FixLabel = $"Optional: install {preferred.ToolName} for more accurate beats.";
            FixCommand = fix;
        }
    }

    private string NameOf(string capability) => capability switch
    {
        ToolCapabilities.Beats => "Beats, BPM & key",
        ToolCapabilities.Stems => "Stem separation",
        ToolCapabilities.Transcode => "M4A / AAC playback",
        _ => capability,
    };

    private string GivesOf(string capability) => capability switch
    {
        ToolCapabilities.Beats => "Beatgrid, BPM, waveform and key for every track.",
        ToolCapabilities.Stems => "Stem EQ, stem mutes and vocal regions.",
        ToolCapabilities.Transcode => "M4A and AAC files play. MP3, FLAC and WAV never need it.",
        _ => "Available.",
    };

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
