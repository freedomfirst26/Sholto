using System.ComponentModel;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The system report modal and the boot-time tool health behind the amber dot.</summary>
public interface ISystemReportCoordinator : INotifyPropertyChanged
{
    /// <summary>The system report modal (the amber status dot): one row per external tool and a headline.</summary>
    ISystemReportViewModel SystemReportModal { get; }

    /// <summary>Whether the system report is showing (a pass-through to <see cref="SystemReportModal"/>).</summary>
    bool IsSystemReportOpen { get; }

    /// <summary>Some tool is missing, whatever the controller is doing.</summary>
    bool ToolsMissing { get; }

    /// <summary>The boot-time tool probe's result: keep the health and fill the report.</summary>
    void Report(in SystemCheckReported e);
}
