using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The system report overlay (the amber status dot): one card per capability, a verdict banner on
/// how much of Sholto works. Read-only; a single Close button. Whether the dot is amber, and so whether
/// <see cref="Open"/> is called at all, is <c>MainViewModel</c>'s call.</summary>
public interface ISystemReportViewModel : IModalContent
{
    /// <summary>Hand over the boot-time tool probe's result. Re-probes nothing.</summary>
    void Report(SystemCheckReported report);

    /// <summary>Show the report.</summary>
    void Open();

    /// <summary>Hide the report.</summary>
    void Close();

    /// <summary>One card per capability, in first-seen order; empty until <see cref="Report"/> runs.</summary>
    IReadOnlyList<SystemReportCapability> Capabilities { get; }

    /// <summary>The banner's sentence: the one place the offline / degraded distinction is spelled out.</summary>
    string Headline { get; }

    /// <summary>The banner's bold verdict word(s).</summary>
    string VerdictTitle { get; }

    /// <summary>"N of M ready" over the capabilities.</summary>
    string ReadyCount { get; }

    /// <summary>True when the banner is green.</summary>
    bool IsHealthy { get; }

    /// <summary>True when the banner is amber (degraded or offline).</summary>
    bool NeedsAttention { get; }
}
