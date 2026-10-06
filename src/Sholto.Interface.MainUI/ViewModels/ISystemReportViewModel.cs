using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The system report overlay (the amber status dot): one row per external tool and a headline on
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

    /// <summary>One row per tool; empty until <see cref="Report"/> runs.</summary>
    IReadOnlyList<SystemReportRow> Rows { get; }

    /// <summary>What the report leads with: the one place the offline / degraded distinction shows. It is
    /// also the modal's <see cref="IModalContent.Title"/>.</summary>
    string Headline { get; }
}
